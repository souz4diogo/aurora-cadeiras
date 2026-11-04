import type { AxiosInstance } from 'axios';

import { ApiError, ehErroDeRede, interpretarErro } from '@/core/http/problem';
import type { ClientesRepository } from '@/features/clientes/clientesRepository';
import type { ClientePendente, DadosDoCliente, ResumoDosClientes } from '@/features/clientes/tipos';

/** Limites iguais aos da API (ClienteRequestValidator). */
export const LIMITES_DO_CLIENTE = { nome: 150, email: 150, telefone: 20, cidade: 80 } as const;

export type CampoDoCliente = keyof DadosDoCliente;
export type ErrosDoCliente = Partial<Record<CampoDoCliente, string>>;

export function somenteDigitos(texto: string): string {
  return texto.replace(/\D/g, '');
}

/** Mesma validação da API, no aparelho: o cadastro offline não deve ser recusado depois por algo que já se sabia. */
export function validarCliente(dados: DadosDoCliente): ErrosDoCliente {
  const erros: ErrosDoCliente = {};
  const nome = dados.nome.trim();
  if (nome.length === 0) erros.nome = 'Informe o nome do cliente.';
  else if (nome.length > LIMITES_DO_CLIENTE.nome) {
    erros.nome = `O nome pode ter no máximo ${LIMITES_DO_CLIENTE.nome} caracteres.`;
  }

  const digitos = somenteDigitos(dados.cpfCnpj);
  if (digitos.length !== 11 && digitos.length !== 14) {
    erros.cpfCnpj = 'Informe um CPF (11 dígitos) ou CNPJ (14 dígitos).';
  }

  const email = dados.email.trim();
  if (email.length > 0 && (email.length > LIMITES_DO_CLIENTE.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))) {
    erros.email = 'Informe um e-mail válido.';
  }
  if (dados.telefone.trim().length > LIMITES_DO_CLIENTE.telefone) {
    erros.telefone = `O telefone pode ter no máximo ${LIMITES_DO_CLIENTE.telefone} caracteres.`;
  }
  if (dados.cidade.trim().length > LIMITES_DO_CLIENTE.cidade) {
    erros.cidade = `A cidade pode ter no máximo ${LIMITES_DO_CLIENTE.cidade} caracteres.`;
  }
  if (dados.uf.trim().length > 0 && !/^[A-Za-z]{2}$/.test(dados.uf.trim())) erros.uf = 'A UF deve ter 2 letras.';
  return erros;
}

/** Corpo de POST /clientes. */
interface ClienteRequest {
  nome: string;
  cpfCnpj: string;
  email: string | null;
  telefone: string | null;
  cidade: string | null;
  uf: string | null;
}

/** O que o serviço precisa da API (POST e GET /clientes). */
export interface ClientesApi {
  criar(corpo: ClienteRequest): Promise<void>;
  /** Nome e CPF/CNPJ dos clientes que casam com a busca. */
  buscar(documento: string): Promise<{ nome: string; cpfCnpj: string }[]>;
}

export class ClientesHttpApi implements ClientesApi {
  constructor(private readonly http: AxiosInstance) {}

  async criar(corpo: ClienteRequest): Promise<void> {
    await this.http.post('/clientes', corpo);
  }

  async buscar(documento: string): Promise<{ nome: string; cpfCnpj: string }[]> {
    const { data } = await this.http.get<{ items: { nome: string; cpfCnpj: string }[] }>('/clientes', {
      params: { busca: documento, page: 1, pageSize: 5 },
    });
    return data.items;
  }
}

export interface DependenciasDeClientes {
  repo: ClientesRepository;
  api: ClientesApi;
  novoId: () => string;
  agora?: () => Date;
}

const TAMANHO_DO_LOTE = 20;

function vazioParaNulo(texto: string): string | null {
  const limpo = texto.trim();
  return limpo.length === 0 ? null : limpo;
}

/**
 * Cadastro de cliente pelo Vendedor, offline primeiro (A60, docs/08 §1.1): grava no SQLite e envia depois.
 * O CPF/CNPJ é único no servidor, então um reenvio depois de uma resposta perdida é reconhecido (409) e conferido.
 */
export class ClientesService {
  constructor(private readonly deps: DependenciasDeClientes) {}

  /** Valida e grava no aparelho. Quem chama dispara a sincronização em seguida. */
  async cadastrar(dados: DadosDoCliente): Promise<ClientePendente> {
    const primeiro = Object.values(validarCliente(dados))[0];
    if (primeiro) throw new ApiError('VALIDACAO', primeiro);

    const cpfCnpj = somenteDigitos(dados.cpfCnpj);
    if (await this.deps.repo.existeDocumento(cpfCnpj)) {
      throw new ApiError('DOCUMENTO_DUPLICADO', 'Este CPF/CNPJ já está na fila de envio deste aparelho.');
    }
    const uf = vazioParaNulo(dados.uf);
    const novo = {
      id: this.deps.novoId(),
      nome: dados.nome.trim(),
      cpfCnpj,
      email: vazioParaNulo(dados.email),
      telefone: vazioParaNulo(dados.telefone),
      cidade: vazioParaNulo(dados.cidade),
      uf: uf ? uf.toUpperCase() : null,
      criadoEm: (this.deps.agora?.() ?? new Date()).toISOString(),
    };
    await this.deps.repo.gravar(novo);
    return { ...novo, tentativas: 0, estado: 'Pendente', codigo: null, mensagem: null };
  }

  /** Envia os pendentes em ordem. Rede ou erro do servidor devolvem o cliente à fila e encerram a rodada. */
  async enviarPendentes(): Promise<ResumoDosClientes> {
    const resumo: ResumoDosClientes = { confirmados: 0, rejeitados: 0, interrompidaPorRede: false };
    for (;;) {
      const lote = await this.deps.repo.proximosPendentes(TAMANHO_DO_LOTE);
      if (lote.length === 0) break;
      for (const cliente of lote) {
        if ((await this.enviar(cliente, resumo)) === 'parar') return resumo;
      }
    }
    return resumo;
  }

  private async enviar(cliente: ClientePendente, resumo: ResumoDosClientes): Promise<'seguir' | 'parar'> {
    const { repo, api } = this.deps;
    await repo.marcarEnviando(cliente.id);
    try {
      await api.criar({
        nome: cliente.nome,
        cpfCnpj: cliente.cpfCnpj,
        email: cliente.email,
        telefone: cliente.telefone,
        cidade: cliente.cidade,
        uf: cliente.uf,
      });
      await repo.remover(cliente.id);
      resumo.confirmados++;
      return 'seguir';
    } catch (erro) {
      return this.tratarFalha(cliente, erro, resumo);
    }
  }

  private async tratarFalha(
    cliente: ClientePendente,
    erro: unknown,
    resumo: ResumoDosClientes,
  ): Promise<'seguir' | 'parar'> {
    const { repo } = this.deps;
    if (ehErroDeRede(erro)) {
      await repo.devolverParaFila(cliente.id);
      resumo.interrompidaPorRede = true;
      return 'parar';
    }
    const falha = interpretarErro(erro);
    if (falha.status === undefined || falha.status >= 500) {
      await repo.devolverParaFila(cliente.id);
      return 'parar';
    }
    if (falha.code === 'DOCUMENTO_DUPLICADO' && (await this.jaCadastradoPorEsteEnvio(cliente))) {
      // A resposta do envio anterior se perdeu: o cliente já está no servidor
      await repo.remover(cliente.id);
      resumo.confirmados++;
      return 'seguir';
    }
    await repo.marcarRejeitado(cliente.id, falha.code, falha.message);
    resumo.rejeitados++;
    return 'seguir';
  }

  /** Duplicidade com o mesmo nome: é este mesmo cadastro (reenvio). Nome diferente: é outro cliente. */
  private async jaCadastradoPorEsteEnvio(cliente: ClientePendente): Promise<boolean> {
    try {
      const achados = await this.deps.api.buscar(cliente.cpfCnpj);
      const nome = cliente.nome.trim().toLowerCase();
      return achados.some((c) => somenteDigitos(c.cpfCnpj) === cliente.cpfCnpj && c.nome.trim().toLowerCase() === nome);
    } catch {
      return false;
    }
  }

  listar(): Promise<ClientePendente[]> {
    return this.deps.repo.listar();
  }

  contar() {
    return this.deps.repo.contar();
  }

  reenviar(id: string): Promise<void> {
    return this.deps.repo.reenviar(id);
  }

  descartar(id: string): Promise<void> {
    return this.deps.repo.descartar(id);
  }

  recuperarEnvioInterrompido(): Promise<void> {
    return this.deps.repo.recuperarEnvioInterrompido();
  }
}
