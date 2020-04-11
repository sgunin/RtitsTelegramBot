// https://github.com/TelegramBots/Telegram.Bot.Examples/blob/master/Telegram.Bot.Examples.Polling/Program.cs
// https://telegrambots.github.io/book/2/send-msg/photo-sticker-msg.html
// https://github.com/TheSpeedX/PROXY-List/blob/master/socks5.txt
// https://github.com/MihaZupan/HttpToSocks5Proxy

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net.Sockets;
using MihaZupan;
using System.Net.Http;

namespace RtitsTelegramBot
 {
     public class DaemonService : IHostedService, IDisposable
     {
         private const int HEARTBEAT_TIMER_INTERVAL = 30000;

         private readonly ILogger _logger;
         private readonly IOptions<DaemonConfig> _config;
         private readonly Timer _heartbeatTimer;
         private readonly TelegramBotClient _client;
         private readonly CancellationTokenSource _source = new CancellationTokenSource();

         public string BotUserName { get; private set; }
         
         public DaemonService(ILogger<DaemonService> logger, IOptions<DaemonConfig> config)
         {
             _logger = logger;
             _config = config;
             _heartbeatTimer = new Timer(HeartbeatTimerCallback, null, Timeout.Infinite, HEARTBEAT_TIMER_INTERVAL);
             if (!string.IsNullOrEmpty(_config.Value.ProxyHost))
             {
                //var proxy = new WebProxy(_config.Value.ProxyHost, _config.Value.ProxyPort) { UseDefaultCredentials = false };
                var proxy = new HttpToSocks5Proxy(new[] {
                    new ProxyInfo("188.166.83.17", 8080), 
                    });
                _client = new TelegramBotClient(_config.Value.Token, proxy);
                _logger.LogInformation("Create proxing Telegram Bot");
             }
             else
             {
                 _client = new TelegramBotClient(_config.Value.Token);
                 _logger.LogInformation("Create Telegram Bot");
             }
             _source = new CancellationTokenSource();
         }

        private async void InitTelegramBot()
        {
             try
             {
                //ServicePointManager.Expect100Continue = true;
                //ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                
                _logger.LogInformation("Enter GetMeAsync awaiter...");
                //var task = await _client.GetMeAsync();
                //_client.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), _source.Token);
                //_logger.LogInformation("Start receiving telegram message from bot {0} {1}", _client.BotId, task.Username);

                var handler = new HttpClientHandler {  };
                var httpClient = new HttpClient(handler, true);
                var result = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/ip"));
                _logger.LogInformation("HTTPS GET: " + await result.Content.ReadAsStringAsync());
             }
             catch (Exception ex)
             {
                _logger.LogError("Could not connect to Telegram servers. Exception: {0}", ex.Message);
             }
        }

         public Task StartAsync(CancellationToken cancellationToken)
         {
             _logger.LogInformation("Starting daemon");
             _heartbeatTimer?.Change(HEARTBEAT_TIMER_INTERVAL, HEARTBEAT_TIMER_INTERVAL);
             InitTelegramBot();

             return Task.CompletedTask;
         }

         public Task StopAsync(CancellationToken cancellationToken)
         {
             _logger.LogInformation("Stopping daemon");
             _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
             _source.Cancel();
             _client.StopReceiving();

             return Task.CompletedTask;
         }

         public void Dispose()
         {
             _logger.LogInformation("Dispose daemon resources");
             _source.Dispose();
             _heartbeatTimer.Dispose();
         }

         private void HeartbeatTimerCallback(object state)
         {
             long count = GC.GetTotalMemory(false);
             GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
             GC.Collect();
             _logger.LogInformation("Garbage collected {0} byte", count - GC.GetTotalMemory(false));
         }

         private async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
         {
            _logger.LogDebug("HandleUpdateAsync task");
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(update.Message),
                UpdateType.EditedMessage => BotOnMessageReceived(update.Message),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult),
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                _logger.LogDebug("Await handler");
                await handler;
                _logger.LogDebug("End awaiting handler");
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(exception, cancellationToken);
            }
         }

         private async Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
         {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogError(ErrorMessage);             
         }

        private async Task BotOnMessageReceived(Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            switch (message.Text.Split(' ').First())
            {
                // send inline keyboard
                case "/inline":
                    await _client.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                    // simulate longer running task
                    await Task.Delay(500);

                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        // first row
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("1.1", "11"),
                            InlineKeyboardButton.WithCallbackData("1.2", "12"),
                        },
                        // second row
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("2.1", "21"),
                            InlineKeyboardButton.WithCallbackData("2.2", "22"),
                        }
                    });
                    await _client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Choose",
                        replyMarkup: inlineKeyboard
                    );
                    break;

                // send custom keyboard
                case "/keyboard":
                    ReplyKeyboardMarkup ReplyKeyboard = new[]
                    {
                        new[] { "1.1", "1.2" },
                        new[] { "2.1", "2.2" },
                    };
                    await _client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Choose",
                        replyMarkup: ReplyKeyboard
                    );
                    break;

                // send a photo
                case "/photo":
                    await _client.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

                    const string file = @"Files/tux.png";
                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var fileName = file.Split(Path.DirectorySeparatorChar).Last();
                        await _client.SendPhotoAsync(
                            chatId: message.Chat.Id,
                            photo: new InputOnlineFile(fileStream, fileName),
                            caption: "Nice Picture"
                        );
                    }
                    break;

                // request location or contact
                case "/request":
                    var RequestReplyKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        KeyboardButton.WithRequestLocation("Location"),
                        KeyboardButton.WithRequestContact("Contact"),
                    });
                    await _client.SendTextMessageAsync(
                        message.Chat.Id,
                        "Who or Where are you?",
                        replyMarkup: RequestReplyKeyboard
                    );
                    break;

                default:
                    const string usage = "Usage:\n" +
                        "/inline   - send inline keyboard\n" +
                        "/keyboard - send custom keyboard\n" +
                        "/photo    - send a photo\n" +
                        "/request  - request location or contact";
                    await _client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: usage,
                        replyMarkup: new ReplyKeyboardRemove()
                    );
                    break;
            }
        }

        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            await _client.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"Received {callbackQuery.Data}"
            );

            await _client.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                $"Received {callbackQuery.Data}"
            );
        }

        private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
            Console.WriteLine($"Received inline query from: {inlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                // displayed result
                new InlineQueryResultArticle(
                    id: "3",
                    title: "TgBots",
                    inputMessageContent: new InputTextMessageContent(
                        "hello"
                    )
                )
            };

            await _client.AnswerInlineQueryAsync(
                inlineQuery.Id,
                results,
                isPersonal: true,
                cacheTime: 0
            );
        }

        private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
            _logger.LogInformation($"Received inline result: {chosenInlineResult.ResultId}");
        }

        private async Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation($"Unknown update type: {update.Type}");
        }         
     }
 }