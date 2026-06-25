# Руководство по установке перевода

Установка перевода осуществляется вручную и состоит из нескольких простых шагов.

## 1. Подготовка (Установка BepInEx)
Для работы перевода требуется **BepInEx**. Его можно установить через ну например ThunderStore но ниже показан базовый способ ручной установки.

1. Скачайте актуальную версию **BepInEx** с официального GitHub:
   [Скачать BepInEx](https://github.com/bepinex/bepinex)
2. Распакуйте содержимое скачанного архива в корневую папку с игрой. 

В результате корневая папка игры должна выглядеть примерно так:
```text
\BepInEx
\MonoBleedingEdge
\White Knuckle_Data
.doorstop_version
doorstop_config.ini
UnityCrashHandler64.exe
UnityPlayer.dll
White Knuckle.exe
winhttp.dll
```

## 2. Установка перевода
1. Скачайте последний релиз перевода:
   [Скачать WKMSTranslation](https://github.com/MusyaCliento/WKMSTranslation/releases)
2. Распакуйте содержимое архива с переводом по следующему пути:
   `White Knuckle\BepInEx\plugins\WKMSTranslation`

Убедитесь, что внутри папки `WKMSTranslation` находятся следующие файлы:

```text
customfonts
ru.json
WKMSTranslation.dll`
```

## 3. Запуск игры
Очень тяжёлый шаг
Запустите `White Knuckle.exe`. 
Приятной неудачной игры на русском языке!
