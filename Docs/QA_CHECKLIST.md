# چک‌لیست QA و اجرای تست‌ها

## قبل از هر تگ هفته

```
# تست‌های EditMode از خط فرمان (نیازمند license فعال روی همین ماشین)
/home/ha/Unity/Hub/Editor/6000.6.3f1-x86_64/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults ./Logs/editmode.xml -logFile ./Logs/editmode.log

echo "exit=$?"        # صفر = همه سبز
grep -o 'result="[A-Za-z]*"' ./Logs/editmode.xml | sort | uniq -c
```

فعال‌سازی license: `Help ▸ Licensing` یک‌بار در ادیتور گرافیکی. در CI نمی‌رویم (دلیل در `SCOPE_CUTS.md`).

## آیین commit

`git status --short` ← `git add <file...>` ← `git lfs ls-files --summarize` ← `git commit` ← `git push origin main --follow-tags`. جزئیات: [GIT.md](GIT.md).

## باز کردن از clone تازه (هر هفته یک بار، با همین ترتیب)

- [ ] clone در پوشه موقت، باز کردن با Unity 6000.6.3f1
- [ ] import بدون خطا تمام می‌شود؛ Console صفر قرمز
- [ ] `Assets/Scenes/Sandbox.unity` ▸ Play ▸ مکانیک کار می‌کند
- [ ] هیچ «missing script» و هیچ پکیج override در Package Manager نیست

## چک‌لیست داخل بازی (از هفته ۳)

- [ ] کالیبره میکروفون: موج زنده حرکت می‌کند، دکمه TEST یک پینگ واقعی می‌زند، تنظیم بعد از restart می‌ماند
- [ ] Pause: صدا قطع، میکروفون بسته، منو با کیبورد و ماوس کار می‌کند (EventSystem روی `InputSystemUIInputModule`)
- [ ] Retry بعد از مرگ: صحنه reload می‌شود، state دشمن‌ها و HUD تمیز شروع می‌شود (نه ادامه از قبلی)
- [ ] برد: رسیدن به خروجی، رفتن به مرحله بعد، و در مرحله ۴ صفحه پایان
- [ ] متن فارسی در TMP درست و RTL نمایش داده می‌شود و به‌هم‌ریخته نیست
- [ ] HUD از موج‌ها کم‌نورتر است و Bloom آن را درخشان نمی‌کند
- [ ] عملکرد: ۶۰fps روی ماشین هدف با سه پینگ زنده و ۲۰۰ پرتو؛ allocation پایدار در فریم صفر

## چک‌لیست هر مرحله

- [ ] از شروع تا خروج بدون گیرکردن (دیوار نامرئی، مسیر بسته)
- [ ] هر دشمن در مرحله از روی کمان قابل تشخیص است
- [ ] حالت بی‌صدا (فقط Space) برای تمام مرحله‌ها ممکن است
- [ ] مسیر «کم‌سروصدا» برای هر مرحله وجود دارد (پاداش stealth)
- [ ] متن آموزش/راهنما خوانده و فهمیده می‌شود
