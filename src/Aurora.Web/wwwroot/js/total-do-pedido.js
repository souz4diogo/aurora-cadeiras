// Subtotal de cada item e total estimado do pedido, calculados na tela a partir do preço de cada produto
// (atributo data-preco da opção escolhida) e da quantidade. O valor oficial é sempre o que a API calcula ao gravar.
(function () {
  'use strict';

  var formatador = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

  function atualizar() {
    var total = 0;

    document.querySelectorAll('[data-linhas] [data-linha]').forEach(function (linha) {
      var produto = linha.querySelector('select');
      var quantidade = linha.querySelector('input[name$=".Quantidade"]');
      var opcao = produto ? produto.options[produto.selectedIndex] : null;
      var preco = opcao ? parseFloat(opcao.getAttribute('data-preco')) : NaN;
      var unidades = quantidade ? parseInt(quantidade.value, 10) : NaN;
      var subtotal = isFinite(preco) && isFinite(unidades) && unidades > 0 ? preco * unidades : 0;

      var saida = linha.querySelector('[data-subtotal]');
      if (saida) {
        saida.textContent = formatador.format(subtotal);
      }

      total += subtotal;
    });

    var alvo = document.getElementById('total-pedido');
    if (alvo) {
      alvo.textContent = formatador.format(total);
    }
  }

  document.addEventListener('input', atualizar);
  document.addEventListener('change', atualizar);
  document.addEventListener('linhas:alteradas', atualizar);
  document.addEventListener('DOMContentLoaded', atualizar);
})();
