# راه‌اندازی ماشین شما برای باز کردن این پروژه

## ۱) نسخه ادیتور: دقیقاً Unity 6000.6.3f1

پروژه روی `6000.6.3f1` قفل است (`ProjectSettings/ProjectVersion.txt`). این **سیگنال است، نه اجبار**: اگر پروژه را با ادیتور جدیدتر باز کنید، ادیتور فایل‌های YAML را یک‌طرفه ارتقا می‌دهد و نسخه‌های قدیمی‌تر دیگر نمی‌توانند باز کنند. پس:

- Unity Hub ▸ **Install Unity** ▸ نسخه **6000.6.3f1**
- تیک ماژول‌ها: روی لینوکس **Linux Build Support** + **Windows Build Support (Mono)**؛ روی ویندوز **Windows Build Support (Mono)** (پیش‌فرض نصب است)
- ادیتورهای 6000.7، 6000.6.x patch دیگر یا 2022.x را برای این ریپو **به کار نبرید**. اگر مجبورید با نسخه دیگری تست کنید، در یک clone جدا (scratch) کار کنید و **push نکنید**.
- بعد از اولین باز کردن: import چند دقیقه طول می‌کشد. تا پایان آن هیچ‌جا کلیک نکنید و در `Window ▸ Package Manager ▸ Updates` هیچ دکمه‌ای نزنید.
- **Input System (new) تنها حالت فعال است** (`activeInputHandler: 1`). کدهای قدیمی که با `Input.GetAxis` یا `Input.mousePosition` نوشته شده‌اند در این پروژه کامپایل/اجرا نمی‌شوند.

## ۲) گیت و LFS

```
git lfs install --local
```

اگر از قبل clone کرده‌اید و فایل‌ها pointer به‌نظر می‌رسند: `git lfs pull`.

## ۳) ثبت درایور merge فایل‌های YAML

بدون این، mergeهای `.unity` و `.prefab` بی‌صدا خراب می‌شوند.

روی **لینوکس** (مسیر را با نسخه خودتان چک کنید):

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "$HOME/Unity/Hub/Editor/6000.6.3f1-x86_64/Editor/Data/Tools/UnityYAMLMerge merge -p %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

روی **ویندوز** (فقط اگر فایل وجود دارد):

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "\"%PROGRAMFILES%\Unity\Hub\Editor\6000.6.3f1-x86_64\Editor\Data\Tools\UnitySmartMerge.exe\" merge -p %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

روی **مک**: ابزار `UnitySmartMerge` داخل bundle ادیتور است؛ مسیر را با Finder چک و به همان شکل ثبت کنید.

راستی‌آزمایی:

```
git config --get-regexp 'merge\.'      # سه سطر باید باشد
```

یا داخل ادیتور: منوی `Tools ▸ Echo Maze ▸ Validate Git Setup`.

## ۴) باز کردن و اجرا

1. Unity Hub ▸ Add project from disk ▸ پوشه ریشه ریپو (همان‌جا که `Assets/` و `ProjectSettings/` هست).
2. صحنه `Assets/Scenes/Sandbox.unity` را باز کنید (مرحله آزمایشی مکانیک) یا `Assets/Scenes/Boot.unity` را (وقتی ساخته شد: منو و تنظیمات).
3. Play. کنترل‌ها: WASD حرکت، ماوس جهت نگاه، Space پژواک دستی.
4. **روی لینوکس انتظار نداشته باشید میکروفون کار کند.** پشتیبانی `Microphone` در ادیتور Linux مطمئن نیست؛ پروژه برای همین ورودی صدا را پشت لایه `IEchoSource` گذاشته و با `SimulatedEchoSource` (اسلایدر بلندی صدا) کار می‌کند. برای تست میکروفون واقعی، بیلد ویندوز لازم است.

## ۴-الف) ادیتور از خط فرمان (بدون باز کردن پنجره)

برای کامپایل و تست بدون GUI:

```
U=<path-to-editor>/Editor/Unity
"$U" -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/editmode.xml" -logFile "$PWD/Logs/editmode.log"
```

خروجی `0` یعنی همه تست‌ها سبزند؛ نتیجه خط‌به‌خط در `Logs/editmode.xml` است. سه ابزار داخل ادیتور هم همین‌طور صدا زده می‌شوند: `EchoMaze.EditorTools.SandboxSceneSetup.Bootstrap` (ساخت صحنه و assetها)، `.ApplyProjectSettings` (لایه‌ها و sorting)، `EchoMaze.EditorTools.GitSetupValidator.Validate`.

دو تله‌ای که روی این ماشین دیدیم:

- ادیتور کارش را تمام می‌کند و پیام «done» را در log می‌نویسد، ولی **خودش خارج نمی‌شود** و CPU را می‌خورد. یعنی نتیجه نوشته شده؛ با `timeout 400` صدا کنید و بعد از دیدن پیام موفقیت پروسه را `kill -TERM` کنید. فقط توجه کنید چیزهایی که ادیتور موقع exit روی دیسک می‌نویسد (مثل `ProjectSettings/EditorSettings.asset`) در این حالت flush نمی‌شوند.

## ۵) بیلد ویندوز از ادیتور لینوکس

`File ▸ Build Settings ▸ Platform: Windows ▸ Mono` (نه IL2CPP؛ ماژول IL2CPP ویندوز روی لینوکس وجود ندارد). خروجی را داخل `Builds/` بگذارید که ignored است. اولین بیلد ویندوز برنامه‌ریزی‌شده هفته ۳ است.

## ۶) نقش‌های تیم

- کد: حسین عسکری (برنامه‌نویس اصلی) — شاخه‌های `feat/*` و merge به `main`.
- طراحی مرحله/دشمن/بالانس و تست: سید سعید نوریان — پارامترها در `Assets/Data/**` و `Docs/TUNING.md`؛ بدون نیاز به کد.
- تست و بازبین: مهزیار میرعظیمی — فقط تگ‌های `v0.x` را بازی می‌کند و یافته‌اش را در قالب `Docs/PLAYTEST_REPORT.md` ثبت می‌کند.
