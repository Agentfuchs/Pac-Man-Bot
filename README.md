# Pac-Man Bot for Discord

![Discord Bots](https://discordbots.org/api/widget/status/398127484983443468.svg) ![Discord Bots](https://discordbots.org/api/widget/servers/398127484983443468.svg?noavatar=true)  
![Discord Bots](https://discordbots.org/api/widget/lib/398127484983443468.svg?noavatar=true) ![Discord Bots](https://discordbots.org/api/widget/owner/398127484983443468.svg?noavatar=true)  
[![paypal](https://img.shields.io/badge/Donate-PayPal-green.svg)](http://paypal.me/samrux)  

Играйте в лучшие чат-игры для Discord: Pac-Man, Uno, Hangman, Pets и другие! Работает на сервере с друзьями или в личных переписках с ботом.
Цель состоит в том, чтобы доставить больше удовольствия вам и вашей группе с наименьшим количеством хлопот и спама.

Особенности следующих игр:
* Uno: играйте с 10 друзьями и ботами в классической карточной игре Uno.
* Hangman: каждый должен угадать случайное слово или выбранное вами слово!
* Вакагочи: наслаждайтесь уходом за питомцем в клоне тамагочи на основе Discord.
* ReactionRPG: сражайтесь с монстрами и становитесь сильнее или бросайте вызов своим друзьям на битву - наслаждайтесь этой простой ролевой игрой в чате!
* Tic-Tac-Toe, Connect Four: бросьте вызов своим друзьям или самому боту.
* Code Break: разгадайте секретный код в этой игре-головоломке.
* Сапер: версия сапёра с использованием функции спойлера.
* Кубик Рубика: попытка собрать куб в форме чата. Шутки в сторону.
* Pac-Man: пошаговое управление в упрощенном, но верном воспроизведении аркадной игры. Оригинальная игра PacManBot.

[**Сервер поддержки тут**](https://discord.gg/hGHnfda)  

&nbsp;

## Компиляция

Если вы хотите скомпилировать Pac-Man Bot, вот основные шаги.
 
Чтобы скомпилировать бота, вам необходимо установить [.NET 5.0 SDK здесь](https://dotnet.microsoft.com/download/dotnet/5.0). В Windows Visual Studio должна установить его за вас. 

Перед компиляцией вам необходимо добавить пакет NuGet библиотеки DSharpPlus, предварительно добавив источник nuget: https://nuget.emzi0767.com/. Если вы используете IDE, например Visual Studio или Rider, вы можете добавить DSharpPlus через нее.
Для компиляции используйте файл `Publish.bat`, который содержит эту команду:

    dotnet publish PacManBot.csproj --runtime% RUNTIME% --configuration Release

Где `% RUNTIME%` - это система, для которой вы будете компилировать, например, linux-x64 или win-x64. Для Raspberry Pi используйте linux-arm.
Команда сгенерирует папку bin /Release/net5.0/% RUNTIME%/publish/, содержащую всю программу.


### Использование эмоций с сервера

Я рекомендую использовать эти эмоций которые есть на [сервере Pac-Man Bot](https://discord.gg/hGHnfda).

1. Возьмите изображения эмоций из папки [_Resources/Emotes/](https://github.com/OrchidAlloy/Pac-Man-Bot/tree/master/_Resources/Emotes).  
2. Загрузите их на сервер Discord, к которому у вашего бота есть доступ.
3. Получите все их коды. Вы можете сделать это быстро, используя команду разработчика бота «emotes».
4. Измените ваш файл  `src/Constants/CustomEmoji.cs` добавив все новые коды. 
5. Затем вы можете снова собрать бота и проверить, правильно ли отображаются эмоции.

&nbsp;  
&nbsp;  

![Alt](https://raw.githubusercontent.com/Samrux/Pac-Man-Bot/master/_Resources/Avatar.png)
