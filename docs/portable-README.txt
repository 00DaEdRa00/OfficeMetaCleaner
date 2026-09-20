OfficeMetaCleaner — portable (Windows x64, .NET не нужен / no .NET needed)
==========================================================================

[RU]
Этот архив — один из двух:
  OfficeMetaCleaner-win-x64.zip — графическое приложение (GUI).
    Распакуйте и запустите OfficeMetaCleaner.exe, перетащите файлы в окно.
  omc-win-x64.zip — КОНСОЛЬНАЯ утилита для терминала и скриптов.
    Распакуйте и запускайте из командной строки:
      omc clean --help
      omc clean договор.docx --dry-run
      omc clean .\docs --recursive --out .\docs-clean

Язык: авто (на англоязычной Windows — английский, иначе русский),
переопределение: --lang ru|en|auto. Язык GUI — в настройках (шестерёнка),
применяется после перезапуска.
Полная документация: README.md / README.en.md в репозитории.

[EN]
This archive is one of two:
  OfficeMetaCleaner-win-x64.zip — graphical app (GUI).
    Unpack and run OfficeMetaCleaner.exe, then drop files into the window.
  omc-win-x64.zip — CONSOLE tool for terminal and scripts.
    Unpack and run from the command line:
      omc clean --help
      omc clean contract.docx --dry-run
      omc clean .\docs --recursive --out .\docs-clean

Language: auto (English on English Windows, Russian otherwise),
override: --lang ru|en|auto. GUI language is in settings (gear button),
applies after restart.
Full docs: README.md / README.en.md in the repository.
