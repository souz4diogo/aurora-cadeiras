// Comportamento comum a todas as telas (sem scripts embutidos: a política de conteúdo só aceita arquivos do próprio site).
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    // Campos com erro de validação do servidor ficam marcados para leitores de tela
    document.querySelectorAll('.input-validation-error').forEach(function (campo) {
      campo.setAttribute('aria-invalid', 'true');
    });

    // Se a página voltou com erros, leva o foco ao resumo (que é anunciado como alerta) ou ao primeiro campo com erro
    var resumo = document.querySelector('.validation-summary-errors');
    var primeiroCampo = document.querySelector('.input-validation-error');
    if (resumo) {
      resumo.focus();
    } else if (primeiroCampo) {
      primeiroCampo.focus();
    }
  });
})();
