# Pacote MSIX

Este diretório contém o manifesto e os recursos usados para gerar o pacote x64 enviado manualmente ao Microsoft Partner Center.

Pré-requisitos:

- SDK do .NET 10;
- acesso ao NuGet para restaurar as ferramentas oficiais de empacotamento da Microsoft.

Para gerar o pacote:

```powershell
.\packaging\msix\build-msix.ps1
```

O script publica o aplicativo como independente do runtime, monta o pacote e grava o MSIX e seu checksum em `artifacts`.

O MSIX criado para a Microsoft Store não é assinado localmente. A Store assina o pacote depois da certificação. Não adicione certificados ou arquivos `.pfx` ao repositório.
