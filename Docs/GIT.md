# کار با گیت در این ریپو (Unity + LFS)

این فایل قرارداد گیت پروژه پژواک است. سه چیز را باید درست انجام دهید: چیزهای درست را commit کنید، فایل‌های دودوئی (binary) را LFS ببرد، و merge فایل‌های YAML ادیتور را با درایور درست انجام دهید.

## چه چیزی commit می‌شود و چه چیزی نمی‌شود

**همیشه commit:**

- `Assets/**` به‌همراه **همه فایل‌های `.meta`** (بدون `.meta` گودپاشی، GUIDها از بین می‌روند و مرجع‌ها در صحنه می‌شکنند)
- `Packages/manifest.json` و `Packages/packages-lock.json`
- `ProjectSettings/**` (از جمله `ProjectSettings/Packages/**`)
- `.gitignore`، `.gitattributes`، `Docs/**`

**هرگز commit نمی‌شود** (همه از قبل در `.gitignore` هستند؛ فقط چک کنید که چیزی به زور `git add -f` نشده باشد):

`Library/`، `Temp/`، `Obj/`، `Logs/`، `UserSettings/`، `Builds/`، `MemoryCaptures/`، `Recordings/`، `*.csproj`، `*.sln`، `*.slnx`، `*.user`، `sysinfo.txt`، `InitTestScene*.unity*`

بیلد نهایی بازی **داخل ریپو نمی‌رود**؛ zip تحویل جداگانه فرستاده می‌شود.

## آیین commit (هر بار، بدون استثنا)

```
git status --short                    # ۱. چه چیزی واقعا تغییر کرده؟
git add <path> <path>                 # ۲. فایل به فایل، نه git add -A
git lfs ls-files -n                   # ۳. هر png/ttf/otf/wav/ogg جدید باید اینجا باشد
git commit -m "..."                   # ۴
git push origin main --follow-tags    # ۵ (فقط روی شاخه‌های خودتان بدون --follow-tags)
```

قدم ۳ حیاتی است: در این ریپو یک بار LFS **بی‌صدا اجرا نشد** و یک PNG ۸۱۶٬۴۶۴ بایتی خام در تاریخ commit شد. اگر فایلی در قدم ۳ نیست و باید باشد:

```
git rm --cached <file>
git lfs install --local
git add <file>
```

پیام commit: `feat:` فیچر، `fix:` باگ، `chore:` زیرساخت، `test:` تست، `docs:` سند. پیشوندهای دامنه‌ای هم مجاز است: `chore(project-settings):`، `feat(sandbox):`.

## شاخه‌ها و تگ‌ها

- `main` **همیشه با ادیتور 6000.6.3f1 باز و قابل Play است.** تسترها و بازبین‌ها فقط `main` یا یک تگ را می‌گیرند، نه شاخه کاری.
- کار روی `feat/echo-mesh`، `feat/enemy-crawler`، … با عمر حداکثر **۲ روز**؛ merge به‌صورت squash.
- پایان هر هفته یک تگ: `v0.1-sandbox`، `v0.2-arc-train`، `v0.3-mic-calibration`، `v0.4-enemies`، `v0.5-submission`، `v0.6-final`.
- `main` هرگز `--force` push نمی‌شود. بعد از اولین clone توسط تیم، `git lfs migrate` و هر نوع بازنویسی تاریخ **ممنوع** است.
- ویرایش‌های `ProjectSettings/**` (لایه‌ها، کیفیت، input handler، enter-play-mode) در یک commit جداگانه و دسته‌ای می‌آیند، نه وسط یک فیچر برنچ.
- هر شاخه حداکثر **یک صحنه** را ویرایش کند. صحنه‌ها بزرگ‌ترین منبع تعارض‌اند.

## درایور merge فایل‌های YAML (Unity Smart Merge)

`.gitattributes` روی تمام فایل‌های YAML (`*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, …) مقدار `merge=unityyamlmerge` را خواسته است. اگر این درایور در تنظیمات گیتِ ماشین شما ثبت **نشده باشد**، گیت بی‌صدا به merge متنی ساده برمی‌گردد و ادغام صحنه را **خراب** می‌کند. ابزار داخل ادیتور است (در Unity 6 حذف نشده)؛ فقط باید ثبتش کنید — راهنما در [TEAM_SETUP.md](TEAM_SETUP.md).

راستی‌آزمایی:

```
git config --get-regexp 'merge\.'     # باید سه سطر unityyamlmerge برگرداند
```

داخل ادیتور هم می‌شود چک کرد: منوی `Tools ▸ Echo Maze ▸ Validate Git Setup` (اسکریپت ادیتور) PASS/FAIL می‌دهد.

## LFS فقط برای باینری

قانون: `*.asset`، `*.unity`، `*.prefab`، `*.meta` **هرگز** به LFS نمی‌روند (کامنت دفاعی هم در `.gitattributes` نوشته شده). این‌ها YAML متن‌اند؛ اگر pointer شوند، ادیتور روی هر ماشینی که `git-lfs` نداشته باشد آن‌ها را به‌عنوان asset می‌خواند و پروژه می‌شکند، و درایور smart merge هم روی pointer کار نمی‌کند.

سقف LFS در گیت‌هاب (پلن رایگان ≈ ۱ گیگ storage/bandwidth) را در نظر داشته باشید: بازی ما خط‌های برداری و اسپرایت‌های کوچک است، پس راحت جا می‌شود — ولی هیچ‌کس PSD بزرگ یا WAV خامِ ضبط‌شده را بدون بررسی داخل `Assets/` نیندازد.
