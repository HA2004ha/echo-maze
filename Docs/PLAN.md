# برنامه توسعه ۵ هفته‌ای (+۱ هفته بافر)

**شروع:** ۱۹ اکتبر ۲۰۲۶ — **تحویل:** ۲۰ نوامبر ۲۰۲۶ — **بافر:** تا ۲۷ نوامبر
**دامنه قطعی:** هسته پژواک + ۳ جنس سطح + ۳ دشمن + تیله صوتی و نبض ضعیف + ۴ مرحله + HUD و منوها + کالیبره میکروفون + صدای کامل + بیلد ویندوز.
**خارج از دامنه:** [SCOPE_CUTS.md](SCOPE_CUTS.md) — این لیست را قبل از افزودن هر فیچر جدید بخوانید.

این فایل **سابقه زنده پروژه** است: پایان هر روز تیک کارها و وضعیت هفته اینجا ثبت و commit می‌شود، و پایان هر هفته همان commit با تگ `v0.x` می‌رود. برای تصمیم‌های فنی: [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md). برای پارامترهای بالانس: [TUNING.md](TUNING.md).

## قوانین ثابت هر فاز

1. پایان هر هفته `main` باید با ادیتور 6000.6.3f1 **باز و با Play قابل اجرا** باشد. اگر چیزی نصفه ماند، پشت یک `bool` در ScriptableObject خاموش می‌شود، نه اینکه پروژه اجرا نشود.
2. معیار هر هفته «تست قابل بازی» است، نه تعداد فیچر.
3. هر روز حداقل یک commit که کامپایل می‌شود.
4. هیچ‌وقت نام فایل یا کلاس `.cs` بیرون از ادیتور عوض نمی‌شود (شکستن مرجع‌ها = صحنه غیرقابل اجرا).
5. فایل‌های `ProjectSettings/**` فقط در commit جداگانه و دسته‌ای ویرایش می‌شوند.

## جدول فازها

| هفته | تاریخ | خروجی | معیار خروج (تست قابل بازی) | تگ |
|---|---|---|---|---|
| ۱ | ۱۹–۲۵ اکتبر | اسکلت + اولین کمان | با WASD حرکت و با Space یک حلقه فیروزه‌ای که حول ستون گود می‌شود، روی دیوار صاف می‌ایستد و در گوشه جعبه نقطه روشن می‌دهد؛ ۶۰fps، صفر خطا | `v0.1-sandbox` |
| ۲ | ۲۶ اکتبر–۱ نوامبر | قطار کمان + جنس‌ها + ابزار دیباگ | یک تستر **حداقل ۳** مورد از {ستون، دیوار، جعبه، فلز، اسفنج} را فقط از کمان نام می‌برد — **نقطه go/no-go پروژه** | `v0.2-arc-train` |
| ۳ | ۲–۸ نوامبر | حلقه صوت + منوها + مرحله ۱ | از منو شروع، کالیبره میکروفون روی ویندوز، رد کردن مرحله ۱ فقط با صدا (و روی لینوکس فقط با Space)، یک بار مردن و Retry، اولین بیلد ویندوز | `v0.3-mic-calibration` |
| ۴ | ۹–۱۵ نوامبر | دشمن‌های خواندنی + تیله صوتی + مرحله ۲ و ۳ | در مرحله ۳ فقط از روی کمان بگو خزنده است یا خارپشت، با تیله یکی را دور کن و به خروجی برس | `v0.4-enemies` |
| ۵ | ۱۶–۲۰ نوامبر | بلعنده + مرحله ۴ + صدا + تحویل | هر ۴ مرحله از سر تا پایان روی ویندوز با میکروفون واقعی و ۶۰fps؛ یک هم‌تیمی از clone تازه طبق README بالا بیاید | `v0.5-submission` |
| ۶ | ۲۱–۲۷ نوامبر (بافر) | burn-down باگ‌ها، re-tune خوانایی، ضبط demo | نسخه نهایی ارائه | `v0.6-final` |

## لیست cut از پیش توافق‌شده (اگر هر هفته عقب افتاد)

- **هفته ۱:** حذف ماسک شکستگی و lookup جنس (فقط `d` خام). **هرگز حذف نشود:** mesh builder، ScriptableObject پارامترها، درز `IEchoSource`.
- **هفته ۲:** حذف اسفنج و رسیدن به دو جنس. اگر تست خوانایی خورد، هفته ۳ کامل به خوانایی می‌آید و گیم‌پلی شروع نمی‌شود.
- **هفته ۳:** حذف CREDITS و متن پایان؛ حذف نشانگر بلندی voice (درِ نویز می‌ماند). **کالیبره هرگز حذف نمی‌شود.**
- **هفته ۴:** خارپشت = همان خزنده با امضا و رفتار ساده‌تر؛ هشدار نزدیکی فقط متن.
- **هفته ۵:** به این ترتیب: موانع مرحله ۳ کمتر ← بلعنده فقط «حفره» بدون پاس occlusion ← پایان به‌صورت صفحه متن. **مرز تعهد: ۴ مرحله — پولیش را cut کن، مرحله را نه.**

---

# هفته ۱ — چک‌لیست روزانه

هدف تنها: یک حلقه فیروزه‌ای که با فرمول GDD حول ستون/دیوار/جعبه جمع می‌شود، با ۶۰fps و صفر خطا، و ریپویی که تیم بتواند باز کند.
**عمداً ساخته نمی‌شود (تا دامنه نلغزد):** قطار کمان، ماسک شکستگی، جنس‌های فلز/اسفنج، میکروفون، HUD، منو، دشمن، مرحله.

### روز ۱ — زیرساخت و اسناد

- [ ] ثبت درایور merge و `git lfs install --local` و راستی‌آزمایی با `git config --get-regexp merge\.`
- [ ] `.gitattributes`: `text eol=lf` برای `*.md/*.json/*.cs/*.shader/*.hlsl` و کامنت دفاعی «Unity YAML هرگز LFS نمی‌شود»
- [ ] `Docs/`: GDD، PLAN، SCOPE_CUTS، GIT، TEAM_SETUP، README ریشه
- [ ] پاکسازی قالب: حذف `Assets/Welcome`، حذف `com.unity.learn.iet-framework` و فایل Settings آن، rename `SampleScene` → `Sandbox`
- [ ] ساختار پوشه‌ها و چهار `.asmdef` (Runtime / Editor / EditMode / PlayMode)

### روز ۲ — تنظیمات پروژه و صحنه

- [ ] `Project Settings > Editor`: خاموش‌کردن Enter Play Mode Options (Domain Reload برگردد)
- [ ] لایه‌ها: `World, EchoTarget, Enemy, Absorber, Player, Trigger` — Sorting: `Default ← Echo ← HUD`
- [ ] صحنه `Sandbox`: دوربین ارتو ۶، پس‌زمینه مشکی، تیک Post Processing، حذف Global Light 2D
- [ ] Global Volume + `EchoVolumeProfile.asset` با override فقط Bloom
- [ ] سه شیء تست روی لایه `EchoTarget` (ستون / دیوار / جعبه) و GameObject `EchoField`
- [ ] `PlayerMock`: حرکت WASD، زاویه از نشانگر ماوس، Space → پینگ

### روز ۳ — ورودی و پرتوها

- [ ] `EchoTuningProfile` + نمونه `Data/Echo/EchoTuning.asset` (با fallback اگر asset نبود)
- [ ] `IEchoSource` + `SimulatedEchoSource` + `ScriptedEchoSource`
- [ ] `PingRecord` + `RayFan` با `Physics2D.RaycastNonAlloc`

### روز ۴ — هندسه و رندر

- [ ] `ArcGeometry` (کلاس ایستا و خالص، فرمول `r_k(θ) = R_k + A_k (d(θ) - d_min)`)
- [ ] `EchoRenderer` با Mesh ظرفیت ثابت + `IEchoRenderer`
- [ ] `EchoDirector`: نگهداری pooled سه پینگ، `ForcePing`، مرگ کمان در `R <= rKill`
- [ ] `EchoArc.shader` (additive، `_Hdr`، دامن نرم) + متریال `Arc_Cyan`

### روز ۵ — تست و بستن هفته

- [ ] `Tests/EditMode/ArcMathTests.cs`: `Formula_MatchesGdd`، `MonotonicInDistance`، `CollapsesToOrigin_AsRAge`، `AllMisses_ProduceNoVertices_AndNoNaN`، `GainSign_FlipsDeformation`
- [ ] اجرای تست‌ها در Test Runner (و از خط فرمان با `-runTests -testPlatform EditMode`)
- [ ] `Docs/TUNING.md` با جدول پارامترها
- [ ] آیین commit ([GIT.md](GIT.md)) ← تگ `v0.1-sandbox` ← push با `--follow-tags`

### معیار خروج هفته ۱

- [ ] clone تازه با 6000.6.3f1 باز می‌شود و `Sandbox` بدون خطا Play می‌شود
- [ ] حلقه فیروزه‌ای در ستون گود می‌شود، روی دیوار صاف است، در گوشه جعبه نقطه روشن دارد
- [ ] `ArcGeometry.Build` زیر ۳ms در Profiler؛ صفر خطا در Console
- [ ] `git lfs ls-files --summarize` باینری‌ها را لیست می‌کند؛ `merge.unityyamlmerge.driver` ثبت است
- [ ] اگر حلقه دیده نشد، به این ترتیب عیب‌یابی: تیک Post Processing دوربین ← لایه `EchoTarget` و ماسک پرتو ← `sortingLayer` و متریال ← `_Hdr` نسبت به `threshold` بلوم

---

# هفته ۲ — قطار کمان و آزمون خوانایی

کار: قطار کمان (`arcInterval` / `arcsPerPing` / `A_k` با `innerDecay` / `fadeCurve`)، ماسک شکست پایدار به‌ازای هر پینگ + gapهای با seed، SO سه جنس، اعتبارسنجی «نقطه گوشه»، `EchoDebugWindow` (Force Ping، رسم خام پرتوها در Scene view، نمودار `d[i]` و `amp[i]`، freeze کمان، زمان‌سنج و allocation)، تست‌های EditMode ریاضی و شکست، گالری اشیاء در `Sandbox`، `Docs/TUNING.md`.
معیار: go/no-go خوانایی. اگر خورد، همه‌چیز متوقف و روی خوانایی کار می‌شود.

# هفته ۳ — حلقه صوت، کالیبره، منوها، مرحله ۱

کار: `MicrophoneEchoSource` + `LoudnessFollower` + `NoiseGate`، صفحه کالیبراسیون با موج زنده، Options با PlayerPrefs، بازنویسی `InputSystem_Actions.inputactions` و `GameInput` (جانشینی `PlayerMock`)، Shift بی‌صدا، پژواک دستی + cooldown ring، HUD کامل، `GameFlow` + Pause، پنل برد/باخت، `Boot.unity` و `Level01_QuietRoom`، **اولین بیلد ویندوز Mono و اولین تست جدی میکروفون روی ویندوز** (`Docs/MIC_TEST_MATRIX.md`).

# هفته ۴ — دشمن‌ها و تیله صوتی، مرحله ۲ و ۳

کار: `NoiseBoard`، تابع خالص `HeardBy` با جریمه line-of-sight + تست، `EnemyBrain` (Patrol/Investigate/Chase/Lost)، امضای موجی خزنده و خارپشت به‌صورت SO، هشدار نزدیکی، تیله صوتی با پینگ از مبدأ خودش، نبض ضعیف، `Interactable/Door/Exit`، `Level02_Corridor` کامل، ژئومتری `Level03_Arena`.

# هفته ۵ — بلعنده، مرحله ۴، صدا، تحویل

کار: بلعنده (trigger + پاس occlusion + بی‌تفاوت به تله)، `Level04_Finale` با گذرگاه «تیله ضروری»، سطوح کیفیت (تعداد پرتو/Bloom/MSAA)، صدا و موسیقی کامل با AudioMixer و Snapshot توقف، بالانس از حداقل سه تست، `Docs/QA_CHECKLIST.md` برای هر ۴ مرحله، بیلد ویندوز + zip، تگ `v0.5-submission`، ضبط ۶۰ ثانیه demo.

# هفته ۶ — بافر

burn-down لیست باگ تسترها، re-tune خوانایی، Level Select دیباگ؛ هر آزمایش اختیاری (کمان روی GPU) پشت `#if` تا `main` قابل تحویل بماند.
