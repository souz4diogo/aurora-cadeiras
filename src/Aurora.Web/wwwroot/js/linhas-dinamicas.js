// Linhas dinâmicas de formulário (itens da ficha técnica, itens do pedido).
//
// Marcação esperada:
//   <tbody data-linhas data-prefixo="Itens"> <tr data-linha> ...campos name="Itens[0].Campo" id="Itens_0__Campo"... </tr> </tbody>
//   <template data-modelo> <tr data-linha> ...campos com o índice "__INDICE__"... </tr> </template>
//   <button type="button" data-adicionar-linha>  e, em cada linha, <button type="button" data-remover-linha>
//   <div id="anuncio-linhas" aria-live="polite">  (anúncio para leitores de tela)
//
// Depois de adicionar ou remover, os índices são renumerados (0, 1, 2...), que é o que o model binding do MVC exige.
// Sem scripts embutidos: a política de conteúdo só aceita arquivos do próprio site.
(function () {
  'use strict';

  var ATRIBUTOS = ['name', 'id', 'for', 'aria-describedby', 'data-valmsg-for'];

  function escapar(texto) {
    return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  function renumerar(container) {
    var prefixo = escapar(container.getAttribute('data-prefixo'));
    var comColchetes = new RegExp(prefixo + '\\[(\\d+|__INDICE__)\\]', 'g');
    var comSublinhado = new RegExp(prefixo + '_(\\d+|__INDICE__)__', 'g');
    var nome = container.getAttribute('data-prefixo');

    container.querySelectorAll('[data-linha]').forEach(function (linha, indice) {
      linha.querySelectorAll('*').forEach(function (elemento) {
        ATRIBUTOS.forEach(function (atributo) {
          var valor = elemento.getAttribute(atributo);
          if (valor) {
            elemento.setAttribute(
              atributo,
              valor.replace(comColchetes, nome + '[' + indice + ']').replace(comSublinhado, nome + '_' + indice + '__')
            );
          }
        });
      });
    });
  }

  function anunciar(texto) {
    var regiao = document.getElementById('anuncio-linhas');
    if (regiao) {
      regiao.textContent = texto;
    }
  }

  function adicionar(botao) {
    var formulario = botao.closest('form') || document;
    var container = formulario.querySelector('[data-linhas]');
    var modelo = formulario.querySelector('template[data-modelo]');
    if (!container || !modelo) {
      return;
    }

    container.appendChild(modelo.content.cloneNode(true));
    renumerar(container);

    var linhas = container.querySelectorAll('[data-linha]');
    var nova = linhas[linhas.length - 1];
    var primeiroCampo = nova.querySelector('select, input');
    if (primeiroCampo) {
      primeiroCampo.focus();
    }

    document.dispatchEvent(new CustomEvent('linhas:alteradas', { detail: { container: container } }));
    anunciar('Linha ' + linhas.length + ' adicionada. Total de linhas: ' + linhas.length + '.');
  }

  function remover(botao) {
    var linha = botao.closest('[data-linha]');
    var container = linha.closest('[data-linhas]');
    var formulario = container.closest('form') || document;
    linha.remove();
    renumerar(container);

    var restantes = container.querySelectorAll('[data-linha]');
    // O foco volta para um lugar previsível, em vez de se perder com a linha removida
    var alvo = formulario.querySelector('[data-adicionar-linha]');
    if (alvo) {
      alvo.focus();
    }

    document.dispatchEvent(new CustomEvent('linhas:alteradas', { detail: { container: container } }));
    anunciar('Linha removida. Total de linhas: ' + restantes.length + '.');
  }

  document.addEventListener('click', function (evento) {
    var alvo = evento.target;
    if (!(alvo instanceof Element)) {
      return;
    }

    var adicionarBotao = alvo.closest('[data-adicionar-linha]');
    if (adicionarBotao) {
      evento.preventDefault();
      adicionar(adicionarBotao);
      return;
    }

    var removerBotao = alvo.closest('[data-remover-linha]');
    if (removerBotao) {
      evento.preventDefault();
      remover(removerBotao);
    }
  });
})();
