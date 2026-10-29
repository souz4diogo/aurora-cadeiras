# 01 - Divisão do Grupo e Plano de Trabalho

**Projeto:** PIM IV - Fábrica de Cadeiras (Aurora Cadeiras)
**Grupo:** 4 integrantes (o manual permite até 6, da mesma turma)
**Status:** proposta para validação do grupo

> ⚠️ **Os nomes abaixo são provisórios.** "Aluno 2, 3 e 4" simulam os colegas; troquem pelos
> nomes e RAs reais e ajustem a divisão conforme a afinidade de cada um.
> Segundo o manual, **somente o líder** acessa o sistema de trabalhos da UNIP, monta o grupo
> relacionando os demais integrantes e posta o PIM.

---

## 1. Integrantes e papéis

| Integrante | Papel no grupo | Papel no Scrum (Etapa 9) |
|---|---|---|
| **Aluno 1 (Diogo)** | **Líder** - posta o PIM, entrega a cópia impressa, integra o documento | Desenvolvedor (banco e arquitetura) |
| **Aluno 2** | Responsável técnico de backend e infraestrutura | Desenvolvedor (API, web, nuvem) |
| **Aluno 3** | Responsável por mobile, acessibilidade e formatação ABNT | Desenvolvedor (mobile) |
| **Aluno 4** | Responsável por negócio, gestão ágil e textos de abertura/fechamento | **Scrum Master** (e simula o Product Owner junto com o professor) |

---

## 2. Quem faz o quê (todas as etapas do manual)

| Etapa do manual | Disciplina | Autor | Revisor |
|---|---|---|---|
| 1 - Caracterização da organização | - | Aluno 4 | Aluno 1 |
| 2 - Planejamento da solução | Empreendedorismo em TI | Aluno 4 | Aluno 3 |
| 3 - Responsabilidade social e diversidade | Relações Étnico-Raciais e Culturas Afro-Indígenas | Aluno 3 | Aluno 4 |
| 4 - Solução Web | Desenvolvimento Web com .NET | Aluno 2 | Aluno 1 |
| 5 - Solução Mobile | Desenvolvimento Mobile | Aluno 3 | Aluno 2 |
| 6 - Arquitetura de Software | Programação Aplicada em .NET | Aluno 1 | Aluno 2 |
| 7 - Projeto do Banco de Dados | Programação de Banco de Dados | Aluno 1 | Aluno 2 |
| 8 - Infraestrutura em Nuvem e DevOps | Cloud Computing e DevOps | Aluno 2 | Aluno 1 |
| 9 - Gerenciamento Ágil | Gerenciamento de Projetos Ágil | Aluno 4 | Aluno 3 |

**Revisão cruzada:** ninguém entrega uma etapa sem que o revisor tenha lido e conferido
contra o manual (`03-checklist-manual.md`).

### Partes do documento fora das etapas

| Parte | Responsável | Observação |
|---|---|---|
| Capa e Folha de rosto | Aluno 3 | Seguir os modelos do manual (4 nomes + RAs) |
| Resumo (150 a 500 palavras) e palavras-chave | Aluno 4 | Escrever **por último** |
| Abstract (em um só idioma) e keywords | Aluno 3 | Tradução do Resumo |
| Sumário | Aluno 3 | Gerado automaticamente; sem listas, resumos e apêndices |
| Introdução (objetivo, metodologia, apresentação da empresa) | Aluno 4 | Escrever depois do desenvolvimento |
| Conclusão (singular, retoma as propostas da Introdução) | Aluno 4 | Revisão do Aluno 1 |
| Referências (ABNT) | Aluno 3 | Cada autor envia suas fontes em ABNT |
| Integração final e formatação ABNT | **Aluno 1** (líder) | Aluno 3 apoia |
| Postagem e cópia impressa | **Aluno 1** (líder) | - |

### Protótipos (para gerar prints reais)

| Protótipo | Responsável |
|---|---|
| Script SQL e diagrama de Chen (banco) | Aluno 1 |
| API REST e painel web mínimo | Aluno 2 |
| App mobile mínimo (apontamento com QR code) | Aluno 3 |
| Quadro Kanban e gráficos de backlog | Aluno 4 |

---

## 3. Orçamento de páginas (Desenvolvimento: mínimo 20, máximo 30)

| Etapa | Páginas-alvo |
|---|---|
| 1 | 2 |
| 2 | 3 |
| 3 | 2 |
| 4 | 3 |
| 5 | 3 |
| 6 | 3 |
| 7 | 4 |
| 8 | 3 |
| 9 | 3 |
| **Total** | **26** (margem de 4 páginas para ajustes) |

Resumo, Abstract, Sumário, Introdução e Conclusão ficam **fora** da contagem do Desenvolvimento.
Diagramas e tabelas contam como página; comprimam com cuidado para não ficar abaixo de 20.

---

## 4. Plano de trabalho do grupo (para terminar o PIM)

> ⚠️ **Prazo oficial ainda não informado.** O plano abaixo usa semanas relativas;
> preencham as datas assim que o grupo souber a data de postagem.
> Este plano é do **trabalho acadêmico**. O planejamento ágil da **solução** (Etapa 9)
> está em `02-backlog-agil.md`.

| Semana | Marco | Responsáveis |
|---|---|---|
| 1 | Escopo validado pelo grupo; repositório criado; Etapas 1, 2 e 3 em rascunho; script SQL e diagrama de Chen | Todos |
| 2 | Etapas 4, 5, 6 e 7 em rascunho; protótipos mínimos rodando para gerar prints | Alunos 1, 2 e 3 |
| 3 | Etapas 8 e 9; diagramas finais; revisão cruzada de todas as etapas | Todos |
| 4 | Introdução, Conclusão, Resumo, Abstract, Referências; formatação ABNT; teste antiplágio; **postagem e impressão** | Líder (Aluno 1) coordena |

Reserve **as últimas 48 horas** só para revisão, ajuste de páginas e postagem. O manual
informa que **não há exceção de prazo** e que envio fora do sistema da UNIP reprova.

---

## 5. Regras do grupo

1. **Textos autorais.** Cada autor escreve com as próprias palavras; todo trecho de terceiros
   leva citação e referência em ABNT (o manual reprova plágio integral, parcial, conceitual e
   autoplágio, e a UNIP usa software de comparação).
2. **Um repositório Git** para o projeto, com a pasta `docs` para os textos em Markdown.
3. **Um arquivo por etapa**, nomeado `E01-...`, `E02-...`, para facilitar a integração.
4. **Fontes em ABNT desde o início:** quem consulta, anota autor, título, ano, local, URL e data de acesso.
5. **Reunião curta semanal** (15 a 30 min) e canal de mensagens do grupo para dúvidas.
6. **Não alterar o texto de outro integrante sem avisar;** sugestões vão para o autor.
7. **Padrão visual:** diagramas e tabelas numerados e com legenda ("Figura 1 - ...", "Tabela 1 - ..."), como o manual faz com a Tabela 1.

---

## 6. Pontos para o grupo decidir

- [ ] Nomes e RAs dos 4 integrantes e quem é o líder.
- [ ] Data de postagem e data da entrega impressa.
- [ ] Cidade do polo para a capa.
- [ ] Orientador(a) (campo da folha de rosto).
- [ ] Tecnologias confirmadas com os professores (SGBD, mobile, nuvem).
