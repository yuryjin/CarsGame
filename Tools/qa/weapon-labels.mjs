// Real arsenal shortcuts: verify all fifty selected weapons remain readable.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/weapon-labels';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], failures = [], errors = [];
try {
  for (const lang of (process.env.CG_HUD_LANGS || 'ru,en,tr,zh,de,es,fr,pt,uk').split(',')) {
    const context = await browser.newContext({ viewport: { width: 640, height: 360 }, hasTouch: true });
    try {
      // Controlled UI fixture, never used as earned campaign/progression evidence.
      await context.addInitScript(lang => localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify({ version: 5, updatedAtUnixMs: Date.now(), settings: { lang, hintsSeen: 65535 }, progress: { fullArsenal: true, weapons: [3,3,3,3,3] } }) })), lang);
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      await page.goto(process.env.URL || 'http://127.0.0.1:8806/?sdk=mock');
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      const renderer = await page.evaluate(() => { const g=document.querySelector('canvas').getContext('webgl2'),e=g.getExtension('WEBGL_debug_renderer_info'); return g.getParameter(e ? e.UNMASKED_RENDERER_WEBGL : g.RENDERER); });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(160);
        const s=await page.evaluate(() => {const s=window.CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s;});
        assert.equal(s.error || '', ''); return s;
      };
      await cmd('touch','on'); await cmd('goto','garage');
      // The ordinary race can kill an idle car mid-probe. The actual garage
      // test drive gives its moving target a firing grace period, without
      // changing player health, input handling or weapon-selection code.
      let entered = false;
      for (let attempt=0;attempt<20;attempt++) {
        const b=(await cmd('ui')).buttons.find(b=>b.name==='TestDrive');
        assert(b?.interactable, 'Garage test drive is available');
        if (b.y>.16 && b.y<.94) {
          const box=await page.locator('canvas').boundingBox();
          await page.mouse.click(box.x+b.x*box.width,box.y+b.y*box.height);
          entered=true; break;
        }
        await page.mouse.move(448,216); await page.mouse.wheel(0,b.y>.5?200:-200);
        await page.waitForTimeout(180);
      }
      assert(entered, 'Reach the actual test-drive button');
      for (let i=0;i<70 && (await cmd('state')).race!=='Racing';i++) await page.waitForTimeout(100);
      const initial=await cmd('state');
      assert.equal(initial.evt,'garage_test');
      assert(initial.fullArsenal, 'Fixture must enable the full arsenal');
      const checkReticle=process.env.CG_RETICLE==='1';
      if(checkReticle){await page.keyboard.down('Space');await page.waitForTimeout(1000);}
      for (let group=0;group<5;group++) {
        if (group) {
          await page.keyboard.down('q'); await page.waitForTimeout(80);
          await page.keyboard.press('Tab', { delay: 80 }); await page.waitForTimeout(80);
          await page.keyboard.up('q'); await page.waitForTimeout(80);
        }
        for (let slot=0;slot<10;slot++) {
          await page.keyboard.press(slot===9?'0':String(slot+1), { delay: 80 }); await page.waitForTimeout(120);
          const state=await cmd('ui'), index=group*10+slot;
          assert(state.health>0 && state.evt==='garage_test', 'Label fixture remains alive in the test drive');
          if (state.activePrimary !== index) {
            await fs.writeFile(`${out}/shortcut-failure.json`,JSON.stringify({lang,index,state},null,2));
            await page.screenshot({path:`${out}/shortcut-failure.png`});
          }
          assert.equal(state.activePrimary,index, `Real shortcut ${lang} weapon ${index}`);
          const name=state.labels.find(l=>l.name==='ActiveWeapon'), ammo=state.labels.find(l=>l.name==='Ammo');
          evidence.push({lang,index,name,ammo,renderer,reticle:state.reticle});
          if(checkReticle) {
            const areaOrTrap=[21,24,30,35,40,41,42,43,44,45,46].includes(index);
            const contact=index>=10&&index<=28 || [31,37,38,39,47].includes(index);
            assert(state.reticle,'Build exposes the actual HUD marker');
            if(areaOrTrap)assert.equal(state.reticle.visible,false,`No forward marker for area/trap weapon ${index}`);
            else if(contact)assert(state.reticle.visible && state.reticle.x>=0 && state.reticle.x<=1 && state.reticle.y>=0 && state.reticle.y<=1,`Reach-based marker stays in view: ${lang} ${index}`);
          }
          for(const [label,min] of [[name,14],[ammo,12]]) {
            if(!label || label.truncated || label.fontPixels<min-.1 || label.lines>2 || !label.renderedText)
              failures.push({lang,index,label});
          }
          if(process.env.CG_GARAGE_BOUNDS==='1') {
            const back=state.buttons.find(b=>b.name==='ReturnToGarage');assert(back);
            for(const label of [name,ammo])
              assert(!(Math.abs(back.x-label.x)*640<(back.width+label.width)/2-1 && Math.abs(back.y-label.y)*360<(back.height+label.height)/2-1),`Garage return overlaps ${lang} weapon ${index} ${label.name}`);
          }
          if (['ru','zh'].includes(lang) && [0,49].includes(index)) await page.screenshot({path:`${out}/${lang}-${index}.png`});
        }
      }
      console.log('CHECKED',lang,50,'weapons');
    } finally { await context.close(); }
  }
  assert.deepEqual(errors,[]); assert.equal(failures.length,0, `${failures.length} unreadable labels; see evidence.json`);
  console.log('WEAPON LABELS PASS',evidence.length);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,failures,errors},null,2));
  await browser.close();
}
