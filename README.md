<p align="center">
  <b><a href="README.md">🇧🇷 Português</a></b> &nbsp;•&nbsp; <b><a href="README.en.md">🇺🇸 English</a></b>
</p>

<br> <br> 

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="src/NovaLite.Desktop/Assets/NovaHub_White_Icon.png">
    <source media="(prefers-color-scheme: light)" srcset="src/NovaLite.Desktop/Assets/NovaHub_Black_Icon.png">
    <img src="src/NovaLite.Desktop/Assets/NovaHub_Black_Icon.png" alt="Ícone do NovaHub" width="300">
  </picture>
</p>

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="src/NovaLite.Desktop/Assets/NovaHub_Logo_White_No_Icon.png">
    <source media="(prefers-color-scheme: light)" srcset="src/NovaLite.Desktop/Assets/NovaHub_Logo_Black_No_Icon.png">
    <img src="src/NovaLite.Desktop/Assets/NovaHub_Logo_Black_No_Icon.png" alt="NovaHub" width="500">
  </picture>
</p>

<p align="center">
  Central gratuita, moderna e open source para testar, calibrar e monitorar controles GameSir Nova Lite no Windows.
</p>

<p align="center">
  <a href="https://github.com/otaviossousa/NovaHub/releases/latest"><img alt="Baixar versão mais recente" src="https://img.shields.io/badge/baixar-versão%20mais%20recente-b91c1c?style=for-the-badge&logo=windows"></a>
</p>

<p align="center">
  <img alt="Windows 10 e 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows">
  <img alt="Licença MIT" src="https://img.shields.io/badge/Licença-MIT-green">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet">
</p>

---

## Sobre o projeto

**NovaHub** é uma central para Windows feita para quem usa o **GameSir Nova Lite** e quer ter mais controle sobre o próprio controle.

Em uma única interface, você pode testar os comandos em tempo real, conferir a precisão e a circularidade dos analógicos e testar a vibração de cada motor.

O **NovaHub** foi pensado para ser **leve, simples e discreto**. Você pode conectar até **4 controles ao mesmo tempo**, cada um em sua própria aba, e deixar o aplicativo rodando em segundo plano pela bandeja do Windows, acompanhando o status e a bateria dos dispositivos sem precisar manter a janela aberta.

O **NovaHub** é **100% gratuito, portátil, open source, sem anúncios e sem telemetria**.

Feito para testar, acompanhar e aproveitar melhor o seu controle.


> O NovaHub é um projeto independente e comunitário de código aberto, sem vínculo comercial com a GameSir.

---

## Interface

A interface do **NovaHub** foi projetado para acompanhar as diferentes variantes do GameSir Nova Lite.

![Prévia animada dos temas e recursos do NovaHub](docs/novahub-themes.gif)

### Monitoramento discreto na bandeja 

Ao minimizar ou fechar a janela principal, o aplicativo vai para a área de **ícones ocultos do Windows**. Pelo menu de contexto, é possível acompanhar a qualquer momento quantos controles estão emparelhados e o nível de carga de cada um, sem interferir na sua jogatina.

<p align="center">
  <img src="docs/novahub-tray.png" alt="Monitoramento de controles pelo NovaHub nos ícones ocultos do Windows" width="360">
</p>

---

## Bateria e modos de conexão

O GameSir Nova Lite pode ser emparelhado ao Windows em diferentes modos de operação. Por isso, a forma como a bateria e o transporte são apresentados varia conforme a tecnologia ativa:

![Demonstração dos níveis de bateria e modos de conexão no NovaHub](docs/novahub-battery-modes.gif)

### Como cada modo se comporta

* **Dongle USB 2,4 GHz**: Nesse modo o controle comunica-se via XInput. A bateria é classificada nas categorias oficiais do padrão: **Vazia**, **Baixa**, **Média** ou **Cheia**.
* **Cabo USB Direto**: Modo cabeado de menor latência possível e alimentação constante. O controle permanece energizado durante o uso.
* **Bluetooth (Switch, DualShock 4 ou Android)**: Em conexões Bluetooth compatíveis, o NovaHub lê relatórios estendidos HID para identificar o dispositivo e reportar a bateria quando disponível pelo firmware.
* **Política de Integridade**: O NovaHub prioriza fontes de dados oficiais. Se determinado modo de conexão não disponibilizar telemetria confiável, o aplicativo apresenta o status como indisponível (`?`).

---

## Como baixar e usar

### Opção 1: Microsoft Store

O projeto está disponivel na **Microsoft Store**, acesse o [NovaHub](https://apps.microsoft.com/detail/9NDD0QW2VXMS?hl=pt-br&gl=BR&ocid) diretamente da loja oficial no seu PC e aproveite.

<p align="center">
  <img src="docs/MS_NovaHub.png" alt="Pagina do NovaHub na Microsoft Store">
</p>


### Opção 2: Pacote Portátil

1. Acesse a página de [Releases oficiais](https://github.com/otaviossousa/NovaHub/releases/latest).
2. Baixe o arquivo `NovaHub-<versão>-win-x64.zip`.
3. Extraia o conteúdo para a pasta de sua preferência.
4. Execute `NovaHub.exe`.

> [!TIP]
> O executável é 100% autocontido: não requer instalador e não precisa de instalação separada do .NET Runtime. É compatível com Windows 10 e Windows 11 de 64 bits.

> [!NOTE]
> O Windows pode exibir um aviso preventivo do SmartScreen na primeira execução por se tratar de um executável open source sem certificado pago. Certifique-se de baixar o pacote sempre através da página oficial de Releases deste repositório.

---

## Privacidade e segurança

* **Zero telemetria**: Nenhuma métrica de uso, diagnóstico ou leitura é gravada externamente.
* **Segurança local**: Todas as leituras e preferências de idioma e tema permanecem salvas exclusivamente no seu computador.
* **Sem alterações invasivas**: O NovaHub não modifica o firmware do controle nem sobrescreve memórias de calibração permanente.
* **Transparência de rede**: Links externos (manual oficial, perfil do autor e repositório) só são acessados quando você clica deliberadamente neles nas configurações.

---

## Compilar o código-fonte

Se desejar inspecionar o código, contribuir ou gerar sua própria compilação local:

### Pré-requisitos

* Windows 10 ou Windows 11 (64 bits);
* SDK do .NET 10 instalado;
* PowerShell 5.1 ou superior.

### Compilação e testes

Clone o repositório e execute no PowerShell:

```powershell
.\build.ps1
```


Para gerar o mesmo pacote ZIP portátil disponibilizado nas Releases oficiais:

```powershell
.\build.ps1 -Package
```

O ZIP portátil e seu checksum SHA-256 são salvos em `artifacts/<versão>/portable/`.
O pacote MSIX da Store e seu checksum ficam em `artifacts/<versão>/store/`.

---

## Documentação oficial do controle

Para consultar manuais de fábrica, instruções de calibração manual e procedimentos de emparelhamento por hardware, visite o [Manual oficial do GameSir Nova Lite](https://gamesir.com/pt-BR/support/manuals/gamesir-nova-lite).

---

## Contribuir com o projeto

O NovaHub é open source. Você pode estudar o código, criar um fork e fazer adaptações para suas próprias necessidades. Issues, sugestões e pull requests são bem-vindos; melhorias alinhadas ao objetivo do aplicativo podem ser revisadas e incorporadas ao projeto.

Leia o [guia de contribuição](CONTRIBUTING.md) antes de enviar uma alteração.

## Desenvolvedor e apoio

Desenvolvido por [Otavio Sousa](https://github.com/otaviossousa).

Se o projeto foi útil para você e quiser contribuir com algum valor, fique à vontade. A contribuição é totalmente opcional e o NovaHub continuará gratuito: [GitHub Sponsors](https://github.com/sponsors/otaviossousa).

## Licença

Distribuído sob a [Licença MIT](LICENSE). Você pode usar, copiar, modificar e redistribuir o código, desde que mantenha o aviso de copyright e os termos da licença.
