# Nacle Foto

**Português (Brasil)** | [English](../README.md)

Um app Windows rápido e leve para **marcar fotos** em pastas enormes, **fechar uma
marcação por casamento** e **enviar para a gráfica** — sem instalar nada, sem copiar
arquivo e sem criar pasta.

![A tela de marcação](screenshots/02-selecao.png)

---

## O que ele faz

- **Marca fotos** em pastas com milhares de arquivos, mostrando os dados da câmera (EXIF)
  de cada uma
- **Guarda uma marcação por casamento** — a mesma pasta pode ter vários casamentos
- **Fecha a marcação** e envia: pelo WhatsApp, por link de download ou preparando os
  arquivos
- **Envia os arquivos originais** — sem cópia, sem hard link, sem pasta extra, com a
  qualidade original
- **Dois idiomas** (português e inglês), seguindo o idioma do computador por padrão

---

## Instalar

**Copie a pasta, abra o .exe.** Não precisa instalar nada: o app usa o .NET Framework 4.8
que já vem no Windows 10 e 11. O app inteiro tem cerca de 1 MB.

1. Copie a pasta `NacleFoto` para o computador (por exemplo, `C:\NacleFoto`)
2. Dê dois cliques em `NacleFoto.exe`
3. Abra **Configurações** e coloque o telefone da gráfica (só números, com DDI e DDD,
   por exemplo `5511999999999`)
4. Abra **Configurações → WhatsApp (WAHA) → Gerenciar o WhatsApp**, instale o servidor do
   WhatsApp com um clique e aperte **Subir o servidor**
5. Aperte **QR code** e leia com o celular (WhatsApp → Aparelhos conectados)

Guias completos, passo a passo:

- **[Português — COMO-INSTALAR.txt](COMO-INSTALAR.txt)**
- **[English — INSTALL.txt](INSTALL.txt)**

O servidor do WhatsApp (WAHA) é baixado pelo próprio app na primeira vez, então ninguém
digita comando nenhum.

---

## Telas

| | |
|---|---|
| ![Lista de casamentos](screenshots/01-casamentos.png) | ![Marcação](screenshots/02-selecao.png) |
| ![Zoom](screenshots/03-zoom.png) | ![Tela de envio](screenshots/04-envio.png) |
| ![Configurações](screenshots/05-config.png) | ![Marcação em inglês](screenshots/06-selection-en.png) |
| ![Depois de fechar a marcação](screenshots/07-depois-de-fechar.png) | ![Gerenciando o servidor do WhatsApp](screenshots/08-waha.png) |

---

## Decisões de arquitetura

| Decisão | Motivo |
|---|---|
| **C# / WPF em .NET Framework 4.8** | Roda em qualquer Windows 10/11 **sem instalar nada** (o .NET Framework já vem no Windows). Exe de ~1 MB. |
| **Sem Electron / sem Python** | 150 MB de RAM e startup lento — pesado demais para uma máquina antiga. |
| **WIC para imagem** | `BitmapImage.DecodePixelWidth` faz *decode escalado*: miniatura de uma foto de 24 MP sem carregar 24 MP na memória. |
| **EXIF em parser próprio (gerenciado puro)** | Lê só o cabeçalho do JPEG → indexar 2.000 fotos leva segundos, sem decodificar. |
| **Cache de miniaturas em disco, auto-indexado** | O nome do arquivo é o hash de (caminho, tamanho, data): arquivo alterado gera miniatura nova, sem tabela para invalidar. |
| **Cache chaveado por serial do volume** | Pendrive muda de letra (E: → F:) mas o serial é o mesmo → as miniaturas reaparecem. |
| **Seleção por caminho relativo** | A marcação sobrevive à mudança de letra do pendrive. |
| **`.nproj` salvo junto das fotos** | A marcação viaja junto das fotos, mesmo entre máquinas diferentes. |
| **Grade = linhas + `UniformGrid` + `VirtualizingStackPanel`** | Virtualização nativa do WPF: só as miniaturas visíveis existem na tela. |
| **Link de download via sendit.sh** | Sem login para criar o link e sem login para quem recebe. |
| **Envio direto do arquivo original** | Nada é copiado e nenhuma pasta é criada — nem hard link. |
| **O servidor do WhatsApp roda local** | O app instala, sobe e desliga. Sem conta, sem serviço, sem custo. |

---

## Envio pelo WhatsApp (WAHA)

O app fala com o **WAHA**, um servidor de API do WhatsApp aberto, por HTTP no localhost.
O app cuida de tudo:

1. Verifica se o **Node** está instalado; se não, instala pelo **winget**
2. Instala o **WAHA** (git clone + `yarn install` + build) em
   `%LOCALAPPDATA%\NacleFoto\Waha` — sem Chromium (o motor NOWEB não precisa), economizando
   ~150 MB
3. **Sobe o servidor em localhost** (com uma chave de API gerada pelo próprio app)
4. Mostra o **QR code dentro do app**: leem com o celular, uma vez só, e pronto

Tudo isso em **Configurações → WhatsApp (WAHA) → "Gerenciar o WhatsApp"**, com o
andamento em log. O servidor sobe sozinho quando o app abre e desliga quando ele fecha.

As fotos saem **como arquivo** (não como imagem → qualidade original), direto da pasta
original. **Nada é copiado, nenhuma pasta é criada.**

> Quem lê o QR code é a conta que fica pareada — o app envia por ela. O número da gráfica
> é só o destinatário.

---

## Estrutura do projeto

```
src/NacleFoto.Core/      domínio puro (net48): modelo, parser de EXIF, .nproj, empacotador, SigV4, i18n
src/NacleFoto.Windows/   camada Windows (net48): WIC, cache, área de transferência, integração com o Explorer
src/NacleFoto.App/       interface WPF (net48)
tests/NacleFoto.Tests/   xunit (net48)
tools/NacleFoto.Shots/   harness de desenvolvimento: gera fotos de teste com EXIF e renderiza as telas em PNG
docs/                   guias de instalação (PT/EN) e telas
```

## Como compilar, testar e rodar

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build NacleFoto.sln
dotnet test  NacleFoto.sln
dotnet run --project src/NacleFoto.App
```

O exe fica em `src/NacleFoto.App/bin/Debug/net48/NacleFoto.exe`.

Para gerar a pasta que se copia para o computador:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1        # -> dist\NacleFoto
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -Zip   # -> dist\NacleFoto.zip
```

## Renderizar as telas sem abrir o app (desenvolvimento)

```powershell
dotnet run --project tools/NacleFoto.Shots
# gera PNGs em C:\NacleDemo\nacle-shots e fotos de teste em C:\NacleDemo\nacle-demo
```

---

## Configurações

- **Idioma**: automático (segue o computador) / português / inglês
- **Telefone da gráfica** (só números, com DDI e DDD) — ex.: `5511999999999`
- **Formato do envio**: fotos soltas / compactado (.zip) / os dois
- **Tamanho máximo por parte** (MB) — padrão 95
- **Pasta de destino** dos arquivos preparados
- **Política de retenção**: depois de N dias (padrão 365) a marcação antiga sai da lista.
  As fotos no disco nunca são tocadas.
- **Incluir fotos que ainda estão na nuvem** (OneDrive) — desligado por padrão
- **Link de download**: onde guardar o link (nenhum / sendit.sh / pasta sincronizada)
- **Fotos por mensagem** no envio automático — padrão 50

## O fluxo de envio

**Nada é duplicado no disco e nenhuma pasta é criada.** O botão principal da tela de envio
é **"Enviar pelo WhatsApp (automático)"**:

1. O **WAHA** (servidor do WhatsApp) guarda a sessão: QR code lido uma vez
2. O app manda as fotos **como arquivo** (qualidade original), direto do disco.
   **Nada é copiado, nenhuma pasta é criada.**
3. Progresso na tela ("Enviando 50 de 300...")

Os outros dois botões são para o caso manual:

- **Gerar link de download** — um clique: o link aparece escrito **e já copiado**
  (Ctrl+V no WhatsApp). Padrão: sendit.sh (grátis, sem login).
- **Abrir a conversa no WhatsApp** — abre a conversa no WhatsApp **Web** (não o app, para
  não perguntar toda vez se quer abrir o aplicativo).

### Vários casamentos na mesma pasta

Uma pasta por semana, vários casamentos dentro. Cada marcação vira um item da lista:
`dir`, `dir (2)`, `dir (3)`...
Para enviar vários de uma vez: marque os fechados na lista (Ctrl+clique) e clique em
**"Enviar os selecionados"** — na tela de envio, cada um tem um quadradinho para
escolher quais entram.

## Atalhos e interações

**Na grade:**

| Ação | Como |
|---|---|
| Marcar / desmarcar | clicar no **quadradinho** (canto da foto) ou no **botão direito** |
| Abrir em tela cheia | **clicar na foto** ou teclar **Enter** |
| Marcar a foto focada | **Espaço** ou **X** |
| Desfazer | **Ctrl+Z** |

No zoom: botão **Marcar/Desmarcar** (visível), tecla `X`, `←`/`→` navegam, `F` ajusta à
tela, `Esc` sai.

## Desempenho (medido)

| Situação | Tempo |
|---|---|
| Varrer 3.000 fotos (disco local) | ~85 ms |
| Ler EXIF de 2.000 fotos | ~6 s (em background, 1 thread) |
| Varrer `OneDrive\Imagens\dir` (tudo na nuvem) | 24 ms (as na nuvem são puladas) |

Arquivos que ainda estão **na nuvem** (OneDrive) são ignorados de propósito: abrir um deles
dispara o download e trava tudo. Dá para ligar em *Configurações → Incluir fotos que ainda
estão na nuvem*.

## Roteiro

- [x] **F0** Fundação: solução, modelo de `.nproj`, tema
- [x] **F1** Seleção: indexação com EXIF, cache em disco, grade virtualizada, marcação, zoom, desfazer
- [x] **F2** Fechar e enviar: ZIP por partes, fotos soltas, tela de envio
- [x] **F3** Canais: área de transferência, link `wa.me`, link de download (sendit.sh), **envio automático pelo WhatsApp (WAHA)**
- [x] **F4** Configurações + dois idiomas
- [ ] **F5** Integração com o Explorer: menu de contexto, marcar direto do Explorer
- [ ] **F6** Polimento: comparar lado a lado, contact sheet, etiquetas de cor, modo simplificado

## Licença

MIT.
