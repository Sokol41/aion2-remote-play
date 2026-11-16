# aion2-remote-play

**Windows software-input → virtual HID bridge. One button for keyboard and mouse.** Version 6.0; English/Russian UI.

**Мост программного ввода Windows → виртуальные HID-устройства. Одна кнопка для клавиатуры и мыши.** Версия 6.0; интерфейс RU/EN.

[Русская инструкция и техническое описание](README_RU.md) · [English setup and technical description](README_EN.md)

## How it works / Как работает

Captures supported software-injected events through Windows low-level hooks, suppresses their original delivery, and sends held-state reports through HIDMaestro virtual keyboard/mouse devices. Pointer movement stays with the remote-access software. The bridge does not stream video or connect computers.

Перехватывает поддерживаемые injected-события через low-level hooks Windows, подавляет исходную доставку и отправляет состояния клавиатуры/мыши через HIDMaestro. Движение указателя остаётся у программы удалённого доступа. Мост не передаёт видео и не устанавливает соединение между компьютерами.

**Tested stack / Проверенная связка:** Sunshine + Moonlight, Aion 2 (previous bridge version user-tested). V6 compilation and built-in logic/UI-state tests passed; its separate interactive game check is pending.

**Other software / Другой софт:** expected to work if it produces equivalent injected events in the same interactive Windows session. This is an architectural expectation; RDP/AnyDesk/RustDesk/Steam Remote Play have not been tested with the bridge. / Предположительно совместим с ПО, использующим аналогичный injected-ввод в том же интерактивном сеансе Windows. Проверка остальных удалённых клиентов не проводилась.

**Left+right stay left+right. No middle-button emulation or pair delay. Physical middle-click is preserved.** If the source already converts a chord into middle-click, disable that feature at the source.

**ЛКМ+ПКМ остаются ЛКМ+ПКМ. Эмуляции СКМ и ожидания пары нет. Физическая СКМ сохранена.** Если подмена выполняется ещё у источника ввода, отключите её там.

## Run / Запуск

Download the **user ZIP** from a release and extract it completely. Install .NET Desktop Runtime 10 x64 and HIDMaestro 2.1.0 with its driver. Run `Aion2RemotePlay.exe` on the Windows host as administrator and click **Turn on / Включить**. Release keys during the three-second countdown. First test in Notepad using **Advanced → Test: 90 seconds**.

Скачайте и распакуйте **пользовательский ZIP** из релиза, установите .NET Desktop Runtime 10 x64 и HIDMaestro 2.1.0 с драйвером. Запустите `Aion2RemotePlay.exe` на Windows от администратора и нажмите **«Включить»**. Отпустите клавиши на время трёхсекундного отсчёта. Сначала проверьте ввод в Блокноте через **«Дополнительно → Тест: 90 секунд»**.

**Emergency OFF / Аварийное отключение: Ctrl+Alt+Pause.** Ctrl+Alt+ScrollLock is selectable in Advanced. Normal window close also turns the bridge off. / Альтернатива выбирается в «Дополнительно». Штатное закрытие окна также выключает мост.

The source checkout does not include a driver, runtime or prebuilt program. / Архив исходников не включает драйвер, среду .NET или готовую программу. See / См. [BUILDING.md](BUILDING.md).

## Dependencies and credits / Зависимости и авторство

| Project | Role / Роль |
|---|---|
| [HIDMaestro 2.1.0](https://github.com/hifihedgehog/HIDMaestro/releases/tag/v2.1.0) | Virtual HID SDK and devices / SDK и виртуальные HID-устройства |
| [Microsoft .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | C#, Windows Forms, Desktop Runtime |
| [Sunshine](https://github.com/SunshineStream/Sunshine) | Tested remote streaming host / Проверенный сервер трансляции |
| [Moonlight](https://github.com/moonlight-stream/moonlight-qt) | Tested remote client / Проверенный клиент |
| OpenAI Codex | Development/documentation assistance / Помощь в разработке и документации |

MIT for bridge code; preserve upstream [notices](THIRD_PARTY_NOTICES.md) and [licences](licenses/). This is an independent project. / Код моста — MIT; сторонние лицензии и уведомления сохранены. Проект самостоятельный.

Detailed key logging is OFF by default and available only by explicit choice in a 90-second test. Review runtime logs before sharing them. No personal logs, credentials or machine configuration are included in these archives. / Подробная запись кодов клавиш по умолчанию выключена и доступна только по явному выбору в коротком тесте. Проверяйте рабочие журналы перед публикацией. Личные журналы, учётные данные и машинные конфиги в архивы не включены.
