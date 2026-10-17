# پژواک (Echo) — `echo-maze`

بازی مخفی‌کاری/ترسناک دوبعدی top-down که در آن صفحه کاملاً سیاه است و محیط را با **پژواک صدای خودتان** می‌بینید: صدا درمی‌آورید، موج مخروطی به جلو می‌رود، به دیوارها و موجودات می‌خورد و به‌صورت قطار کمان‌های بازگشتی برمی‌گردد؛ شکل هر کمان را باید بخوانید. پروژه درس نظریه بازی (Game Theory)، دانشگاه علم و صنعت ایران.

- سند طراحی: [Docs/GDD.md](Docs/GDD.md)
- برنامه ۵ هفته‌ای و وضعیت: [Docs/PLAN.md](Docs/PLAN.md)
- چیزهایی که عمداً ساخته نمی‌شود: [Docs/SCOPE_CUTS.md](Docs/SCOPE_CUTS.md)
- کار با گیت/LFS: [Docs/GIT.md](Docs/GIT.md) · راه‌اندازی ماشین: [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md)

## نسخه ادیتور

**Unity 6000.6.3f1** — دقیقاً همین نسخه. باز کردن پروژه با ادیتور جدیدتر، فایل‌ها را یک‌طرفه ارتقا می‌دهد. جزئیات و ماژول‌های لازم در [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md).

## باز کردن و اجرا

```
Unity Hub ▸ Add project from disk ▸ این پوشه
Assets/Scenes/Sandbox.unity ▸ Play
```

`Sandbox` صحنه آزمایش مکانیک پژواک است (حرکت با WASD، نگاه با ماوس، پینگ با Space). بیلد ویندوز با پلتفرم **Windows ▸ Mono** گرفته می‌شود.

## ورودی صدا و محدودیت پلتفرم

`Microphone` در ادیتور Unity روی لینوکس قابل اتکا نیست. کل ورودی صدا پشت یک لایه به نام `IEchoSource` است و در حالت توسعه از `SimulatedEchoSource` (اسلایدر/منحنی بلندی صدا) استفاده می‌شود، پس مکانیک روی لینوکس قابل توسعه و دمو است. مسیر میکروفون واقعی با بیلد ویندوز و طبق [Docs/MIC_TEST_MATRIX.md](Docs/MIC_TEST_MATRIX.md) تست می‌شود.

## ساختار پوشه‌ها

```
Assets/Scenes       صحنه‌ها: Sandbox (تست مکانیک)، Boot (منو)، Level01..Level04
Assets/Scripts/Runtime   Core (پژواک) · Audio (ورودی صدا) · Gameplay · Data · UI · Debug
Assets/Scripts/Editor    ابزار ادیتور (دیباگ پژواک، اعتبارسنجی مرحله، Validate Git Setup)
Assets/Tests             EditMode (ریاضی کمان/شنوایی) و PlayMode (اعتبارسنجی صحنه‌ها)
Assets/Data              ScriptableObjectهای بالانس: پارامترهای پژواک، جنس‌ها، دشمن‌ها
Assets/Art               شیدر کمان، متریال‌ها، فونت‌ها
Docs                     GDD، PLAN، SCOPE_CUTS، GIT، TEAM_SETUP، TUNING، DESIGN_DECISIONS، تست‌ها
```

## تیم

حسین عسکری (برنامه‌نویس اصلی — هسته پژواک، ورودی میکروفون، رندر) · مهزیار میرعظیمی (UI، AI، مرحله‌ها، تست) · سید سعید نوریان (طراحی بازی و صدا، بالانس).
