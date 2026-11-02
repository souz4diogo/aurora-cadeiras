import { useAuth, useUsuario } from '@/features/auth/AuthProvider';
import { Botao, EstadoVazio, Tela } from '@/shared/componentes';

/** Perfis que trabalham no painel web (Administrador, Gerente, Almoxarife) não têm tela no app (docs/08 §1). */
export default function NaoSuportado() {
  const usuario = useUsuario();
  const { sair } = useAuth();

  return (
    <Tela>
      <EstadoVazio
        titulo="Este perfil usa o painel web"
        texto={`O app é para operadores, inspetores, vendedores e clientes. ${usuario.nome}, acesse o painel web para fazer o seu trabalho.`}
        acao={<Botao titulo="Sair" variante="secundario" aoPressionar={() => void sair()} />}
      />
    </Tela>
  );
}
