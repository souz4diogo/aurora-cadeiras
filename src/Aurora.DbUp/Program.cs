using DbUp;

// Aplica os scripts numerados de database/ no SQL Server, na ordem, uma única vez cada (journal do DbUp).
// Configuração por variáveis de ambiente, sem segredos no código:
//   ConnectionStrings__Aurora  connection string do banco de destino (obrigatória)
//   SCRIPTS_PATH               pasta dos scripts (padrão: ./database)
const string VariavelDaConexao = "ConnectionStrings__Aurora";
const string VariavelDosScripts = "SCRIPTS_PATH";

var connectionString = Environment.GetEnvironmentVariable(VariavelDaConexao);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Defina a variável de ambiente {VariavelDaConexao}.");
    return 2;
}

var pastaDosScripts = Environment.GetEnvironmentVariable(VariavelDosScripts) ?? Path.Combine(Directory.GetCurrentDirectory(), "database");
if (!Directory.Exists(pastaDosScripts))
{
    Console.Error.WriteLine($"Pasta de scripts não encontrada: {pastaDosScripts}");
    return 2;
}

EnsureDatabase.For.SqlDatabase(connectionString);

var resultado = DeployChanges.To
    .SqlDatabase(connectionString)
    .WithScriptsFromFileSystem(pastaDosScripts)
    .LogToConsole()
    .Build()
    .PerformUpgrade();

if (!resultado.Successful)
{
    Console.Error.WriteLine($"Falha ao aplicar {resultado.ErrorScript?.Name}: {resultado.Error?.Message}");
    return 1;
}

Console.WriteLine("Banco atualizado.");
return 0;
