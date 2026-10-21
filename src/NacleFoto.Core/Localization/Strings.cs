using System;
using System.Collections.Generic;
using System.Globalization;

namespace NacleFoto.Core.Localization
{
    public enum AppLanguage
    {
        Portuguese = 0,
        English = 1
    }

    /// <summary>
    /// Catalogo de textos do programa. Chave -> [Portugues, English].
    /// A ordem das colunas segue AppLanguage.
    /// </summary>
    public static class Strings
    {
        /// <summary>
        /// Language of the operating system.
        ///
        /// Anything that is not Portuguese opens in English. This is the place
        /// where a new language is plugged in when the app supports it.
        /// </summary>
        public static AppLanguage DetectSystemLanguage()
        {
            string culture = "";
            try
            {
                var ci = CultureInfo.CurrentUICulture;
                if (ci != null) culture = ci.Name ?? "";
            }
            catch (Exception) { }
            return DetectLanguage(culture);
        }

        /// <summary>Maps a culture name ("pt-BR", "en-US"...) to a supported language.</summary>
        public static AppLanguage DetectLanguage(string cultureName)
        {
            var name = (cultureName ?? "").Trim();
            if (name.StartsWith("pt", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Portuguese;
            // more languages here when the app supports them
            return AppLanguage.English;
        }
        private static readonly Dictionary<string, string[]> Catalog = new Dictionary<string, string[]>
        {
            // ---------- geral ----------
            { "app.name", new[] { "Nacle Foto", "Nacle Foto" } },
            { "app.ok", new[] { "OK", "OK" } },
            { "app.cancel", new[] { "Cancelar", "Cancel" } },
            { "app.yes", new[] { "Sim", "Yes" } },
            { "app.no", new[] { "Não", "No" } },
            { "app.save", new[] { "Salvar", "Save" } },
            { "app.close", new[] { "Fechar", "Close" } },

            // ---------- tela de casamentos ----------
            { "projects.subtitle", new[] { "Escolha o casamento para marcar as fotos, ou comece um novo.", "Choose the wedding to select photos, or start a new one." } },
            { "btn.newSelection", new[] { "Nova seleção de fotos", "New photo selection" } },
            { "projects.hint", new[] { "Escolha a pasta onde estão as fotos. Você poderá marcar, fechar e enviar depois.", "Pick the folder with the photos. You can select, close and send later." } },
            { "projects.empty", new[] { "Nenhum casamento ainda.\n\nClique em \"Nova seleção de fotos\" para começar. A marcação fica salva, então você pode deixar pela metade e voltar depois.", "No wedding yet.\n\nClick \"New photo selection\" to start. Your selection is saved, so you can leave it half-done and come back later." } },
            { "btn.openSelection", new[] { "Abrir seleção", "Open selection" } },
            { "btn.removeFromList", new[] { "Remover da lista", "Remove from list" } },
            { "projects.count", new[] { "{0} casamento(s)", "{0} wedding(s)" } },
            { "state.open", new[] { "Marcando", "Selecting" } },
            { "state.closed", new[] { "Pronto para enviar", "Ready to send" } },
            { "state.sent", new[] { "Enviado", "Sent" } },
            { "msg.removeConfirm", new[] { "Remover \"{0}\" da lista?\n\nAs fotos e o arquivo de marcação continuam no disco.", "Remove \"{0}\" from the list?\n\nThe photos and the selection file stay on disk." } },

            // ---------- tela de selecao ----------
            { "selection.back", new[] { "Casamentos", "Weddings" } },
            { "selection.close", new[] { "Fechar e preparar envio", "Close and prepare to send" } },
            { "selection.folders", new[] { "Pastas", "Folders" } },
            { "selection.allFolders", new[] { "Todas as fotos", "All photos" } },
            { "btn.addFolder", new[] { "Adicionar outra pasta", "Add another folder" } },
            { "filter.all", new[] { "Todas", "All" } },
            { "filter.marked", new[] { "Marcadas", "Selected" } },
            { "filter.unmarked", new[] { "Não marcadas", "Not selected" } },
            { "btn.markVisible", new[] { "Marcar visíveis", "Select visible" } },
            { "btn.unmarkVisible", new[] { "Desmarcar visíveis", "Unselect visible" } },
            { "btn.markAll", new[] { "Marcar todas", "Select all" } },
            { "btn.clear", new[] { "Limpar", "Clear" } },
            { "selection.title", new[] { "Seleção", "Selection" } },
            { "selection.markedPhotos", new[] { "fotos marcadas", "photos selected" } },
            { "selection.photoData", new[] { "DADOS DA FOTO", "PHOTO DETAILS" } },
            { "selection.noPhoto", new[] { "Clique numa foto da tela para ver os dados dela.", "Click a photo on screen to see its details." } },
            { "btn.undo", new[] { "Desfazer", "Undo" } },
            { "selection.closeHint", new[] { "A marcação fica guardada. Você pode enviar agora, continuar depois ou marcar outro casamento.", "Your selection is saved. You can send it now, continue later, or select another wedding." } },
            { "status.folderCount", new[] { "{0} fotos na pasta  ·  {1} na tela", "{0} photos in the folder  ·  {1} on screen" } },
            { "status.ready", new[] { "{0} fotos  ·  tudo pronto", "{0} photos  ·  all set" } },
            { "status.noPhotos", new[] { "Nenhuma foto encontrada nesta pasta.", "No photos found in this folder." } },
            { "status.scanning", new[] { "Procurando fotos...", "Looking for photos..." } },
            { "settings.cloudFiles", new[] { "Incluir fotos que ainda estão na nuvem (OneDrive). Ligado, o app espera o download de cada arquivo e fica lento.", "Include photos that are still in the cloud (OneDrive). When on, the app waits for each file to download and gets slow." } },
            { "exif.date", new[] { "Data", "Date" } },
            { "exif.camera", new[] { "Câmera", "Camera" } },
            { "exif.lens", new[] { "Lente", "Lens" } },
            { "exif.shutter", new[] { "Obturador", "Shutter" } },
            { "exif.aperture", new[] { "Abertura", "Aperture" } },
            { "exif.iso", new[] { "ISO", "ISO" } },
            { "exif.focal", new[] { "Focal", "Focal" } },
            { "exif.dimensions", new[] { "Dimensões", "Dimensions" } },
            { "exif.gps", new[] { "GPS", "GPS" } },
            { "exif.file", new[] { "Arquivo", "File" } },
            { "exif.folder", new[] { "Pasta", "Folder" } },

            // ---------- mensagens ----------
            { "msg.markAllConfirm", new[] { "Marcar TODAS as {0} fotos desta pasta?\n\nUse se a pasta contém somente um casamento.", "Select ALL {0} photos in this folder?\n\nUse it when the folder holds a single wedding." } },
            { "msg.unmarkAllConfirm", new[] { "Desmarcar todas as fotos?", "Unselect all photos?" } },
            { "msg.closeNone", new[] { "Nenhuma foto foi marcada.\n\nQuer fechar assim mesmo?", "No photo was selected.\n\nClose it anyway?" } },
            { "msg.closeConfirm", new[] { "Fechar a marcação de \"{0}\"?\n\n{1} fotos marcadas ({2})\n\nDepois de fechar, a seleção fica congelada e o app prepara os arquivos para enviar para a gráfica. Você ainda pode reabrir e mudar, mas o app avisa.", "Close the selection of \"{0}\"?\n\n{1} photos selected ({2})\n\nAfter closing, the selection is frozen and the app prepares the files to send to the print shop. You can reopen and change it, but the app will warn you." } },
            { "msg.closed", new[] { "Marcação fechada!\n\n{0} fotos ({1}).\n\nPróximo passo: preparar e enviar para a gráfica.", "Selection closed!\n\n{0} photos ({1}).\n\nNext step: prepare and send to the print shop." } },
            { "msg.saveError", new[] { "Não foi possível salvar a marcação:\n{0}", "Could not save the selection:\n{0}" } },
            { "msg.unexpectedError", new[] { "Ocorreu um problema inesperado:\n\n{0}\n\nSeu trabalho foi salvo. Você pode continuar usando o programa.", "Something went wrong:\n\n{0}\n\nYour work was saved. You can keep using the program." } },

            // ---------- zoom ----------
            { "zoom.marked", new[] { "MARCADA", "SELECTED" } },
            { "zoom.mark", new[] { "Marcar", "Select" } },
            { "zoom.unmark", new[] { "Desmarcar", "Unselect" } },
            { "tile.markTip", new[] { "Marcar ou desmarcar esta foto (ou use o botão direito)", "Select or unselect this photo (or use the right button)" } },
            { "zoom.hints", new[] { "← → navegar   ·   X marcar   ·   Esc sair", "← → navigate   ·   X select   ·   Esc exit" } },

            // ---------- tela de envio ----------
            { "after.title", new[] { "Marcação fechada", "Selection closed" } },
            { "after.question", new[] { "O que quer fazer agora?", "What do you want to do now?" } },
            { "after.sendNow", new[] { "Enviar para a gráfica agora", "Send to the print shop now" } },
            { "after.newSelection", new[] { "Criar outra marcação (outro casamento nesta pasta)", "Start another selection (another wedding in this folder)" } },
            { "after.later", new[] { "Depois (fica guardado aqui)", "Later (it stays saved here)" } },
            { "after.summary", new[] { "{0} fotos marcadas ({1}).", "{0} photos selected ({1})." } },
            { "send.looseHint", new[] { "As fotos marcadas foram colocadas numa pasta de atalhos (NÃO ocupam espaço em disco: apontam para as fotos originais). Abra a pasta, aperte Ctrl+A e arraste para o WhatsApp.", "The selected photos were placed in a shortcut folder (they take NO extra disk space: they point to the original photos). Open the folder, press Ctrl+A and drag them into WhatsApp." } },
            { "send.looseHardLinks", new[] { "{0} atalho(s) - não ocupam espaço em disco", "{0} shortcut(s) - no extra disk space" } },
            { "send.looseHintCopied", new[] { "Algumas fotos had to be copied (estão em outra unidade).", "Some photos had to be copied (they are on another drive)." } },
            { "send.whichOnes", new[] { "O que enviar (marque os casamentos que entram)", "What to send (tick the weddings to include)" } },
            { "send.noneSelected", new[] { "Escolha pelo menos um casamento para enviar.", "Pick at least one wedding to send." } },
            { "send.weddings", new[] { "casamentos", "weddings" } },
            { "send.sendSelected", new[] { "Enviar os selecionados", "Send the selected ones" } },
            { "send.nothingToSend", new[] { "Para enviar, feche a marcação dos casamentos primeiro.", "To send, close the selection of the weddings first." } },
            { "send.title", new[] { "Enviar para a gráfica", "Send to the print shop" } },
            { "send.summary", new[] { "{0} fotos  ·  {1}", "{0} photos  ·  {1}" } },
            { "send.prepare", new[] { "Preparar envio", "Prepare files" } },
            { "send.preparing", new[] { "Preparando arquivos...", "Preparing files..." } },
            { "send.autoWhatsapp", new[] { "Enviar pelo WhatsApp (automático)", "Send through WhatsApp (automatic)" } },
            { "send.autoWhatsappHint", new[] { "O app manda as fotos como arquivo pelo servidor WAHA, direto do disco original: nada é copiado e nenhuma pasta é criada. O WAHA guarda a sessão do WhatsApp (leia o QR code uma vez).", "The app sends the photos as files through the WAHA server, straight from the original folder: nothing is copied and no folder is created. WAHA keeps the WhatsApp session (scan the QR code once)." } },
            { "send.pairWhatsapp", new[] { "Conectar o WhatsApp (QR)", "Connect WhatsApp (QR)" } },
            { "send.wahaNotConfigured", new[] { "O servidor WAHA não está configurado. Vá em Configurações > WhatsApp (WAHA) e coloque o endereço.", "The WAHA server is not configured. Go to Settings > WhatsApp (WAHA) and set the address." } },
            { "send.wahaNotReady", new[] { "O WhatsApp ainda não está pronto (status: {0}). Vá em Configurações > WhatsApp (WAHA) e faça o pareamento com o QR code.", "WhatsApp is not ready yet (status: {0}). Go to Settings > WhatsApp (WAHA) and pair it with the QR code." } },
            { "send.checkingWaha", new[] { "Conferindo o WhatsApp...", "Checking WhatsApp..." } },
            { "qr.title", new[] { "Conectar o WhatsApp", "Connect WhatsApp" } },
            { "qr.scan", new[] { "Leia o QR code com o celular (WhatsApp > Aparelhos conectados).", "Scan the QR code with your phone (WhatsApp > Linked devices)." } },
            { "qr.ready", new[] { "WhatsApp conectado! Pode fechar esta janela.", "WhatsApp connected! You can close this window." } },
            { "qr.unavailable", new[] { "O WAHA não devolveu o QR code (status: {0}).", "WAHA did not return a QR code (status: {0})." } },
            { "qr.serverDown", new[] { "O servidor do WhatsApp não está rodando. Vá em Configurações > Gerenciar o WhatsApp e clique em Subir o servidor.", "The WhatsApp server is not running. Go to Settings > Manage WhatsApp and click Start server." } },
            { "qr.refresh", new[] { "Atualizar", "Refresh" } },
            { "settings.waha", new[] { "WhatsApp (WAHA)", "WhatsApp (WAHA)" } },
            { "settings.wahaManage", new[] { "Gerenciar o WhatsApp", "Manage WhatsApp" } },
            { "waha.title", new[] { "WhatsApp (WAHA)", "WhatsApp (WAHA)" } },
            { "waha.hint", new[] { "O WAHA é o servidor que guarda a sessão do WhatsApp. O programa instala o Node, instala o WAHA e sobe tudo sozinho aqui no computador. A única coisa a fazer é ler o QR code.", "WAHA is the server that holds the WhatsApp session. The program installs Node, installs WAHA and runs it here on this computer. The only thing to do is scan the QR code." } },
            { "waha.nodeStatus", new[] { "Node (motor do WAHA)", "Node (WAHA engine)" } },
            { "waha.wahaStatus", new[] { "Servidor WAHA", "WAHA server" } },
            { "waha.serverStatus", new[] { "Servidor no ar", "Server running" } },
            { "waha.ok", new[] { "instalado", "installed" } },
            { "waha.missing", new[] { "não instalado", "not installed" } },
            { "waha.stopped", new[] { "parado", "stopped" } },
            { "waha.installNode", new[] { "Instalar o Node", "Install Node" } },
            { "waha.install", new[] { "Instalar o WAHA", "Install WAHA" } },
            { "waha.update", new[] { "Atualizar o WAHA", "Update WAHA" } },
            { "waha.start", new[] { "Subir o servidor", "Start server" } },
            { "waha.stop", new[] { "Parar o servidor", "Stop server" } },
            { "waha.log", new[] { "Andamento", "Progress" } },
            { "waha.installingNode", new[] { "Instalando o Node (pode levar alguns minutos)...", "Installing Node (this can take a few minutes)..." } },
            { "waha.installing", new[] { "Instalando o WAHA (pode levar uns 10 minutos)...", "Installing WAHA (this can take about 10 minutes)..." } },
            { "waha.installed", new[] { "WAHA instalado.", "WAHA installed." } },
            { "waha.installFailed", new[] { "FALHOU. As últimas linhas do log estão abaixo:", "FAILED. The last lines of the log are below:" } },
            { "waha.serverFailed", new[] { "O servidor não subiu. As últimas linhas do log estão abaixo:", "The server did not start. The last lines of the log are below:" } },
            { "waha.wrongKey", new[] { "no ar, mas com outra chave. Pare o servidor e suba de novo.", "up, but with another key. Stop the server and start it again." } },
            { "waha.updating", new[] { "Atualizando o WAHA...", "Updating WAHA..." } },
            { "waha.starting", new[] { "Subindo o servidor...", "Starting the server..." } },
            { "waha.updateAvailable", new[] { "Há versão nova do WAHA.", "A new WAHA version is available." } },
            { "settings.wahaUrl", new[] { "Endereço do servidor WAHA", "WAHA server address" } },
            { "settings.wahaKey", new[] { "Chave da API (X-Api-Key)", "API key (X-Api-Key)" } },
            { "settings.wahaSession", new[] { "Nome da sessão", "Session name" } },
            { "settings.wahaAuto", new[] { "Subir o WAHA automaticamente quando o programa abre", "Start WAHA automatically when the program opens" } },
            { "settings.wahaCommand", new[] { "Comando para subir o WAHA", "Command to start WAHA" } },
            { "settings.wahaHint", new[] { "O WAHA é o servidor que cuida do WhatsApp (rode com Docker ou Node). O programa só precisa do endereço. Se o WAHA roda em outro computador, o endereço pode ser http://IP-DO-PC:3000", "WAHA is the server that holds the WhatsApp session (run it with Docker or Node). The program only needs the address. If WAHA runs on another computer, the address can be http://PC-IP:3000" } },
            { "send.sending", new[] { "Enviando {0} de {1}...", "Sending {0} of {1}..." } },
            { "send.sentAll", new[] { "Todas as fotos foram enviadas.", "All photos were sent." } },
            { "send.sentTo", new[] { "Enviado para o WhatsApp ({0}). Se não chegar em 1 minuto, confira se o número está certo.", "Sent to WhatsApp ({0}). If it doesn't arrive within a minute, check that the number is right." } },
            { "send.sendingTo", new[] { "Enviando para:", "Sending to:" } },
            { "send.changeNumber", new[] { "Trocar número", "Change number" } },
            { "send.noNumberSet", new[] { "(nenhum - configure nas Configurações)", "(none - set it in Settings)" } },
            { "send.confirmNumber", new[] { "O WhatsApp ainda não tem conversa com o número {0}.\n\nSe o número estiver errado, as fotos somem sem aviso nenhum. Confirma que esse é o número da gráfica?", "WhatsApp has no conversation with the number {0} yet.\n\nIf the number is wrong, the photos disappear without any warning. Is this the print shop's number?" } },
            { "send.wrongNumber", new[] { "Nada foi enviado. Confira o número da gráfica nas Configurações (agora: {0}).", "Nothing was sent. Check the print shop's number in Settings (currently: {0})." } },
            { "send.needLogin", new[] { "Abri o WhatsApp Web em um navegador separado (o Chrome não deixa automatizar o navegador do dia a dia). Se aparecer o QR code, leia com o celular - só na primeira vez. Depois disso ele fica logado e o app usa essa mesma janela.", "I opened WhatsApp Web in a separate browser (Chrome does not allow automating your everyday browser). If a QR code appears, scan it with your phone - only the first time. After that it stays logged in and the app uses that same window." } },
            { "send.waitingBrowser", new[] { "Abrindo o navegador...", "Opening the browser..." } },
            { "send.errorTitle", new[] { "Não deu para enviar", "Could not send" } },
            { "settings.batchSize", new[] { "Fotos por mensagem no envio automático", "Photos per message in the automatic send" } },
            { "settings.message", new[] { "Mensagem no WhatsApp (deixe vazio para mandar só as fotos)", "WhatsApp message (leave empty to send only the photos)" } },
            { "settings.messageHint", new[] { "Use {wedding} para o nome do casamento e {parts} para a quantidade de lotes.", "Use {wedding} for the wedding name and {parts} for the number of batches." } },
            { "settings.batchSizeUnit", new[] { "fotos por mensagem", "photos per message" } },
            { "send.readyToPaste", new[] { "A primeira parte já está na área de transferência: cole no WhatsApp com Ctrl+V.", "The first part is already in the clipboard: paste it in WhatsApp with Ctrl+V." } },
            { "send.readyToDrag", new[] { "A pasta com as fotos foi aberta no Explorer: aperte Ctrl+A e arraste para o WhatsApp.", "The folder with the photos was opened in Explorer: press Ctrl+A and drag them into WhatsApp." } },
            { "send.alreadySent", new[] { "Esta seleção já foi enviada. Abrir a marcação de novo?", "This selection was already sent. Open the selection again?" } },
            { "send.parts", new[] { "Partes para enviar", "Parts to send" } },
            { "send.partLabel", new[] { "Parte {0} de {1}  ·  {2} fotos", "Part {0} of {1}  ·  {2} photos" } },
            { "btn.copyPart", new[] { "Copiar para o WhatsApp", "Copy for WhatsApp" } },
            { "send.copied", new[] { "Copiado! Agora cole no WhatsApp com Ctrl+V e envie.", "Copied! Now paste it in WhatsApp with Ctrl+V and send." } },
            { "btn.openWhatsapp", new[] { "Abrir WhatsApp", "Open WhatsApp" } },
            { "btn.openFolder", new[] { "Abrir pasta", "Open folder" } },
            { "send.generateLink", new[] { "Gerar link de download", "Create download link" } },
            { "send.openChat", new[] { "Abrir a conversa no WhatsApp", "Open the chat in WhatsApp" } },
            { "send.makingLink", new[] { "Gerando o link...", "Creating the link..." } },
            { "send.linkCopied", new[] { "Link gerado e já copiado (Ctrl+V no WhatsApp).", "Link created and already copied (Ctrl+V in WhatsApp)." } },
            { "send.linkReady", new[] { "Link pronto. Quem receber só precisa clicar e baixar — sem login.", "Link ready. Whoever receives it just clicks and downloads — no login." } },
            { "btn.copyLink", new[] { "Copiar link", "Copy link" } },
            { "btn.markSent", new[] { "Marcar como enviado", "Mark as sent" } },
            { "send.looseTitle", new[] { "Fotos soltas", "Loose photos" } },
            { "send.noSelection", new[] { "Nenhuma foto marcada neste casamento.", "No photo selected in this wedding." } },
            { "send.noLinkProvider", new[] { "O link de download não está configurado.\n\nVocê pode enviar pelo WhatsApp normalmente (botão \"Abrir WhatsApp\"). Se quiser link também, configure em Configurações > Link de download.", "The download link is not configured.\n\nYou can send through WhatsApp as usual (the \"Open WhatsApp\" button). To also have a link, set it up in Settings > Download link." } },
            { "send.noPhone", new[] { "Coloque o telefone da gráfica em Configurações para abrir o WhatsApp.", "Set the print shop phone in Settings to open WhatsApp." } },
            { "send.linkError", new[] { "Não foi possível enviar para o link:\n{0}", "Could not upload for the link:\n{0}" } },
            { "send.sentDone", new[] { "Envio marcado como concluído.", "Send marked as done." } },

            // ---------- configuracoes ----------
            { "settings.title", new[] { "Configurações", "Settings" } },
            { "settings.language", new[] { "Idioma do programa", "Program language" } },
            { "settings.languageAuto", new[] { "Automático (idioma do computador)", "Automatic (system language)" } },
            { "settings.languagePt", new[] { "Português (Brasil)", "Portuguese (Brazil)" } },
            { "settings.languageEn", new[] { "English", "English" } },
            { "settings.phone", new[] { "Telefone da gráfica (WhatsApp)", "Print shop phone (WhatsApp)" } },
            { "settings.phoneHint", new[] { "Só números, com DDI e DDD. Ex.: 5511999999999", "Digits only, with country and area code. E.g. 5511999999999" } },
            { "settings.format", new[] { "Formato do envio", "Send format" } },
            { "settings.formatZip", new[] { "Compactado (.zip)", "Compressed (.zip)" } },
            { "settings.formatLoose", new[] { "Fotos soltas", "Loose photos" } },
            { "settings.formatBoth", new[] { "Os dois", "Both" } },
            { "settings.partSize", new[] { "Tamanho máximo de cada arquivo (MB)", "Maximum size of each file (MB)" } },
            { "settings.partSizeHint", new[] { "O WhatsApp Web aceita até 100 MB por arquivo.", "WhatsApp Web accepts up to 100 MB per file." } },
            { "settings.destFolder", new[] { "Pasta de destino dos arquivos", "Where the files are prepared" } },
            { "settings.link", new[] { "Link de download (avançado)", "Download link (advanced)" } },
            { "settings.linkHint", new[] { "O app envia os arquivos para o seu armazenamento e cria um link. Quem recebe só clica e baixa, sem login nenhum.", "The app uploads the files to your storage and creates a link. Whoever receives it just clicks and downloads, no login at all." } },
            { "settings.endpoint", new[] { "Servidor (endpoint)", "Server (endpoint)" } },
            { "settings.region", new[] { "Região", "Region" } },
            { "settings.bucket", new[] { "Bucket / pasta", "Bucket / folder" } },
            { "settings.accessKey", new[] { "Chave de acesso", "Access key" } },
            { "settings.secretKey", new[] { "Chave secreta", "Secret key" } },
            { "settings.linkEnabled", new[] { "Usar link de download no envio", "Use a download link when sending" } },
            { "settings.provider", new[] { "Onde guardar o link", "Where to host the link" } },
            { "settings.providerNone", new[] { "Nenhum (só WhatsApp)", "None (WhatsApp only)" } },
            { "settings.providerSendit", new[] { "sendit.sh (sem login, 1 dia)", "sendit.sh (no login, 1 day)" } },
            { "settings.providerFolder", new[] { "Pasta sincronizada", "Synced folder" } },
            { "settings.senditHint", new[] { "O app sobe os arquivos para o sendit.sh e cria o link. Sem login, de graça. Atenção: o serviço apaga os arquivos depois de 1 dia e permite 5 downloads - a gráfica precisa baixar logo.", "The app uploads the files to sendit.sh and creates the link. No login, free. Note: the service deletes the files after 1 day and allows 5 downloads - the print shop must download right away." } },
            { "settings.retention", new[] { "Política de retenção", "Retention policy" } },
            { "settings.retentionDays", new[] { "dias", "days" } },
            { "settings.retentionHint", new[] { "Depois de quantos dias a marcação antiga é apagada da lista (as fotos no disco NÃO são tocadas). 0 = nunca apagar.", "After how many days the old selection is removed from the list (the photos on disk are NOT touched). 0 = never remove." } },
            { "settings.syncFolder", new[] { "Pasta sincronizada", "Synced folder" } },
            { "settings.syncHint", new[] { "O app copia os arquivos para essa pasta (que o Google Drive sincroniza) e você compartilha com o botão direito do Explorer. Nenhuma credencial é preciso.", "The app copies the files to this folder (which Google Drive syncs) and you share it with the right-click menu in Explorer. No credentials needed." } },
            { "settings.saved", new[] { "Configurações salvas.", "Settings saved." } },
            { "settings.readmeLanguage", new[] { "O LEIA-ME para a gráfica segue o idioma do programa.", "The readme for the print shop follows the program language." } },

            // ---------- LEIA-ME da grafica ----------
            { "readme.title", new[] { "ENVIO DE FOTOS PARA IMPRESSÃO", "PHOTOS FOR PRINTING" } },
            { "readme.wedding", new[] { "Casamento", "Wedding" } },
            { "readme.client", new[] { "Cliente", "Client" } },
            { "readme.date", new[] { "Data", "Date" } },
            { "readme.photos", new[] { "Fotos", "Photos" } },
            { "readme.sent", new[] { "Enviado", "Sent" } },
            { "readme.howTo", new[] { "COMO IMPRIMIR", "HOW TO PRINT" } },
            { "readme.step1", new[] { "1. Ao receber os arquivos .zip, descompacte todos numa mesma pasta.", "1. When you get the .zip files, extract them all into the same folder." } },
            { "readme.step2", new[] { "2. Se vierem vários arquivos, junte as fotos de todos em uma só pasta.", "2. If you get several files, merge the photos from all of them into one folder." } },
            { "readme.step3", new[] { "3. Envie essa pasta com as fotos em resolução original.", "3. Send that folder with the photos in full resolution." } },
            { "readme.step4", new[] { "4. Para impressão em papel fotográfico, use 20x30 cm (ou 10x15 cm).", "4. For photographic paper, print at 20x30 cm (or 10x15 cm)." } },
            { "readme.step5", new[] { "   O fotógrafo já enquadrou as fotos na proporção 3:2.", "   The photographer already framed the photos in 3:2 ratio." } },
            { "readme.note", new[] { "Observação: as fotos já foram selecionadas pelo fotógrafo.", "Note: the photos were already selected by the photographer." } },
            { "readme.inventory", new[] { "A lista completa dos arquivos está no INVENTARIO.csv.", "The full file list is in INVENTARIO.csv." } },
            { "readme.partsNote", new[] { "O envio foi dividido em {0} partes porque o WhatsApp não aceita arquivos grandes. Junte todas para imprimir.", "The delivery was split into {0} parts because WhatsApp does not accept large files. Merge them all to print." } },

            // ---------- mensagem do WhatsApp ----------
            { "wa.message", new[] {
                "Olá! Segue o envio das fotos selecionadas do {wedding}. {parts}Ao receber, descompacte e imprima em alta resolução. Obrigado!",
                "Hello! Here are the selected photos from {wedding}. {parts}When you receive them, extract and print in high resolution. Thank you!" } }
        };

        private static AppLanguage _language = AppLanguage.Portuguese;

        public static AppLanguage Language
        {
            get { return _language; }
            set
            {
                if (_language == value) return;
                _language = value;
                var h = LanguageChanged;
                if (h != null) h(null, EventArgs.Empty);
            }
        }

        public static event EventHandler LanguageChanged;

        public static string T(string key)
        {
            string[] pair;
            if (!Catalog.TryGetValue(key, out pair)) return key;
            int i = (int)_language;
            if (i >= pair.Length) i = 0;
            return pair[i] ?? pair[0];
        }

        public static string T(string key, params object[] args)
        {
            var s = T(key);
            if (args == null || args.Length == 0) return s;
            try { return string.Format(CultureInfo.CurrentCulture, s, args); }
            catch (FormatException) { return s; }
        }

        public static string NameOf(AppLanguage lang)
        {
            return lang == AppLanguage.English ? "English" : "Portugues (Brasil)";
        }
    }
}
