import { useState } from 'react';

import { interpretarErro } from '@/core/http/problem';
import { useAuth } from '@/features/auth/AuthProvider';

/** Regras do formulário de login (ViewModel da tela): valida no aparelho e traduz o erro do servidor. */
export function useLogin() {
  const { entrar } = useAuth();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [manterConectado, setManterConectado] = useState(true);
  const [enviando, setEnviando] = useState(false);
  const [erroDoEmail, setErroDoEmail] = useState<string | null>(null);
  const [erroDaSenha, setErroDaSenha] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(): Promise<void> {
    const semEmail = email.trim().length === 0;
    const semSenha = senha.length === 0;
    setErroDoEmail(semEmail ? 'Informe o e-mail.' : null);
    setErroDaSenha(semSenha ? 'Informe a senha.' : null);
    setErro(null);
    if (semEmail || semSenha) return;

    setEnviando(true);
    try {
      await entrar(email, senha, manterConectado);
    } catch (falha) {
      setErro(interpretarErro(falha).message);
      setSenha(''); // nunca deixa a senha errada na tela
    } finally {
      setEnviando(false);
    }
  }

  return {
    email,
    setEmail,
    senha,
    setSenha,
    manterConectado,
    setManterConectado,
    enviando,
    erroDoEmail,
    erroDaSenha,
    erro,
    enviar,
  };
}
