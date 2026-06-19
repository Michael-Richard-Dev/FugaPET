# Identidade do Produto — FugaPET

## Nome oficial

**FugaPET — Sistema de Produção e Rastreabilidade PET**

- **Nome curto:** FugaPET
- **Descritivo:** Sistema de Produção e Rastreabilidade PET
- **Empresa responsável:** FUGA COUROS S.A. (o nome da *empresa* é distinto do nome do *produto*)

## Onde usar

Use o nome oficial em:

- README e documentação
- Títulos principais das telas (janela / cabeçalho)
- Mensagens internas voltadas ao operador que se refiram ao produto
- Plano de implantação e materiais de apresentação

## Fonte única (não repita o nome em texto solto)

O nome vive em uma única classe de constantes, `MarcaProduto`:

```csharp
MarcaProduto.Nome          // "FugaPET"
MarcaProduto.Descricao     // "Sistema de Produção e Rastreabilidade PET"
MarcaProduto.NomeCompleto  // "FugaPET — Sistema de Produção e Rastreabilidade PET"
MarcaProduto.Empresa       // "FUGA COUROS S.A."
```

Os títulos das janelas principais (`LoginForm`, `PainelInicialForm`) já usam `MarcaProduto.NomeCompleto`.

## O que NÃO muda nesta etapa

- **Solution / projeto / namespaces** continuam com o nome técnico `FugaPET_Dev`.
- Não há renomeação de arquivos, assembly ou pacotes.
- A adoção é de **nomenclatura/identidade**, não de refatoração estrutural.

## Próximos passos (opcional, fora desta etapa)

- Migrar gradualmente rótulos visuais nos `*.Designer.cs` para refletir o nome onde fizer sentido.
- Avaliar, em momento próprio, a renomeação do assembly/solution.
