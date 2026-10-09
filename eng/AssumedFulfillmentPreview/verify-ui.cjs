const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { chromium } = require('playwright');
const repo = path.resolve(__dirname, '../..');
const r29 = process.env.INPUT_R29 === '1';
const output = path.join(repo, r29 ? 'artifacts/local/neighborhood-simple-input-r29/ui' : 'artifacts/local/assumed-fulfillment-r28/ui');
const base = 'http://127.0.0.1:5400';
const states = ['consent','create','missing-size','empty','accepted','unknown','assigned','in-transit','completed','paused','logout'];
if (r29) states.push('input-goods','input-places','input-time','input-confirm','input-blank');
const sourcePaths = ['eng/AssumedFulfillmentPreview/Program.cs','eng/AssumedFulfillmentPreview/App.razor',
  'Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryAuthoringViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryCreate.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor',
  'Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentStatus.razor'];
if (r29) sourcePaths.push('Ssalddel.Ui.Common/Areas/App/ViewModels/생활배송간편입력ViewModel.cs',
  'Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/생활배송간편입력.razor',
  'Ssalddel/Services/Community/생활배송엑셀초안Service.cs','Ssalddel/Resources/NeighborhoodDelivery/생활배송입력.r29.xlsx');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(path.join(repo, p))).digest('hex');
const hashes = () => sourcePaths.map(p => ({ path:p, sha256:hash(p) }));
const before = hashes();
const report = { sampleOnly:true, realApi:false, apk:false, scenes:[], interactions:[], blockedRequests:[], sourceBefore:before };
fs.mkdirSync(output, { recursive:true });
const ensure = (value, message) => { if (!value) throw Error(message); };
(async () => {
  let fixturePath, formulaPath;
  const templatePath = path.join(repo,'Ssalddel/Resources/NeighborhoodDelivery/생활배송입력.r29.xlsx');
  if (r29) {
    const { FileBlob, SpreadsheetFile } = await import('@oai/artifact-tool');
    const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(templatePath));
    const sheet = workbook.worksheets.getItem('배송입력');
    // Deliberately synthetic, no real account, address, contact, or shipment.
    sheet.getRange('A4:S5').values = [
      [null,'합성 책 상자',1,2,'합성 픽업 장소',null,'검토 이용자','000-0000-0000','합성 전달 장소',null,'검토 수령자','000-0000-0000',
        new Date('2030-01-02T10:00:00Z'),new Date('2030-01-02T12:00:00Z'),'Small',new Date('2030-01-02T10:00:00Z'),new Date('2030-01-02T13:00:00Z'),'합성 시험','오토바이'],
      [null,'보완할 합성 상자',null,null,'합성 픽업 장소',null,'검토 이용자',null,'합성 전달 장소',null,'검토 수령자','000-0000-0000',
        null,null,null,null,null,null,null]
    ];
    workbook.recalculate();
    fixturePath = path.join(output,'input-fixture.xlsx');
    await (await SpreadsheetFile.exportXlsx(workbook)).save(fixturePath);
    sheet.getRange('C4').formulas = [['=1+1']]; workbook.recalculate();
    formulaPath = path.join(output,'formula-fixture.xlsx');
    await (await SpreadsheetFile.exportXlsx(workbook)).save(formulaPath);
  }
  const browser = await chromium.launch({headless:true,channel:'chrome'});
  try {
    for (const width of [320,390]) {
      const context = await browser.newContext({viewport:{width,height:844},locale:'ko-KR',timezoneId:'Asia/Seoul'});
      await context.route('**/*', route => {
        const u = new URL(route.request().url());
        if ((!['data:','blob:'].includes(u.protocol) && u.origin !== base) || u.pathname.startsWith('/api/')) {
          report.blockedRequests.push({external:u.origin !== base,path:u.origin === base ? u.pathname : 'external',method:route.request().method()});
          return route.abort();
        }
        return route.continue();
      });
      for (const state of states) {
        const page = await context.newPage(); const errors=[];
        page.on('pageerror', e => errors.push(e.message));
        await page.goto(`${base}/?state=${state}`, {waitUntil:'networkidle'});
        const expected = state.startsWith('input-') ? ({'input-places':'픽업지','input-time':'언제 받을 수 있나요?','input-confirm':'보낼 내용을 확인하세요'}[state] ?? '어떤 물건을 보내나요?')
          : state === 'logout' ? '로그인이 필요해요' : state === 'consent' ? '배송 정보 이용 안내'
          : ['create','missing-size','empty'].includes(state) ? '자동 배송 접수 조건' : '자동 배송 처리';
        await page.getByRole('heading',{name:expected,exact:true}).waitFor();
        ensure(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),`${state}-${width}: horizontal overflow`);
        ensure(errors.length === 0,`${state}-${width}: ${errors.join(';')}`);
        if (state === 'create') {
          ensure(await page.locator('.auto-fulfillment-conditions fieldset input[type=checkbox]').count() === 6,'Server provider list was not rendered');
          ensure(await page.getByText('배민 연동 가정 · 모의 · 모의',{exact:true}).count() === 1,'Assumed mock label missing');
          await page.locator('#delivery-package-size').selectOption('Large');
          await page.getByRole('heading',{name:'자동 배송 접수 조건',exact:true}).waitFor({state:'hidden'});
          ensure(await page.getByRole('button',{name:'배송 요청 접수하기',exact:true}).count() === 0,'Size change retained confirmation for an old quote');
          await page.getByRole('button',{name:'배송비 확인하기',exact:true}).click();
          await page.getByRole('button',{name:'배송 요청 접수하기',exact:true}).waitFor();
          ensure(await page.getByRole('checkbox',{name:/위 비용·시간·처리처 조건/}).isChecked() === false,'Size change retained automatic acknowledgment');
          report.interactions.push({width,check:'Size invalidates quote and acknowledgment; new quote restores confirmation form'});
        }
        if (state === 'missing-size') ensure(await page.locator('#delivery-package-size').inputValue() === '', 'Missing size was fabricated');
        if (state === 'empty') {
          ensure(await page.locator('.auto-fulfillment-conditions fieldset input[type=checkbox]').count() === 0,'Empty server list used client fallback');
          ensure(await page.getByRole('button',{name:'배송 요청 접수하기',exact:true}).isDisabled(),'Empty providers enabled submission');
        }
        if (state === 'unknown') {
          await page.getByRole('button',{name:'원 요청 결과 확인',exact:true}).click();
          await page.getByText('접수됨',{exact:true}).waitFor();
          report.interactions.push({width,check:'Unknown same-request result confirmation'});
        }
        if (state === 'paused') ensure(await page.locator('.auto-fulfillment-actions button').count() === 0,'Paused feature exposed commands');
        if (state === 'completed') ensure(await page.getByRole('button',{name:'현재 처리처 접수 취소',exact:true}).count() === 0,'Completed state exposed cancellation');
        await page.screenshot({path:path.join(output,`${state}-${width}.png`),fullPage:true});
        report.scenes.push({state,width,errors,horizontalOverflow:false}); await page.close();
      }
      if (r29) {
        const page = await context.newPage(); const errors=[];
        page.on('pageerror', e => errors.push(e.message));
        await page.goto(`${base}/?state=input-blank`, {waitUntil:'networkidle'});
        await page.getByRole('heading',{name:'어떤 물건을 보내나요?',exact:true}).waitFor();
        await page.getByRole('button',{name:'다음 · 장소',exact:true}).click();
        await page.getByRole('alert').waitFor();
        ensure((await page.getByRole('alert').innerText()).includes('중량'),'Missing weight was not requested');
        const importer = page.locator('details.delivery-import');
        await importer.locator('summary').click();
        await page.getByRole('button',{name:'엑셀 양식 준비',exact:true}).click();
        await page.getByRole('link',{name:'양식 내려받기',exact:true}).waitFor();
        ensure((await page.getByRole('link',{name:'양식 내려받기',exact:true}).getAttribute('href')).startsWith('data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,'),'Template was not offered in-memory');
        await page.locator('#delivery-input-file').setInputFiles(templatePath);
        await page.getByText('입력된 업무 행이 없습니다.',{exact:true}).waitFor();
        await page.locator('#delivery-input-file').setInputFiles(formulaPath);
        await page.getByText('수식은 읽지 않습니다. 값만 남긴 양식으로 다시 올려 주세요.',{exact:true}).waitFor();
        ensure(await page.locator('.delivery-import-rows button').count() === 0,'Formula file produced usable rows');
        await page.locator('#delivery-input-file').setInputFiles({name:'oversized.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.alloc(2*1024*1024+1)});
        await page.getByText('2 MiB 이하의 .xlsx 파일 한 개를 선택해 주세요.',{exact:true}).waitFor();
        await page.locator('#delivery-input-file').setInputFiles(fixturePath);
        await page.getByText('4 행 · 합성 책 상자',{exact:true}).waitFor();
        ensure(await page.locator('.delivery-import-rows button').count() === 2,'Actual parser did not produce two rows');
        ensure((await page.locator('[data-input-counters]').innerText()).includes('견적 0 / 등록 0'),'Upload performed a quote or create');
        await page.screenshot({path:path.join(output,`import-rows-${width}.png`),fullPage:true});
        report.scenes.push({state:'import-rows',width,errors:[],horizontalOverflow:false});
        await page.locator('.delivery-import-rows button').nth(1).click();
        await page.getByText('배송입력 5 행의 원본과 보완 항목',{exact:true}).waitFor();
        ensure(await page.locator('#simple-quantity').inputValue() === '0','Missing quantity was invented');
        ensure(await page.locator('#simple-weight').inputValue() === '0','Missing weight was invented');
        await page.getByRole('button',{name:'다음 · 장소',exact:true}).click();
        await page.getByRole('alert').waitFor();
        await page.locator('.delivery-import-rows button').first().click();
        await page.getByText('배송입력 4 행의 원본과 보완 항목',{exact:true}).waitFor();
        await page.getByRole('button',{name:'다음 · 장소',exact:true}).click();
        await page.getByRole('heading',{name:'픽업지',exact:true}).waitFor();
        ensure(await page.locator('#simple-pickup-phone').inputValue() === '000-0000-0000','Phone leading zeros were lost');
        await page.getByRole('button',{name:'이전',exact:true}).click();
        await page.getByRole('heading',{name:'어떤 물건을 보내나요?',exact:true}).waitFor();
        ensure(await page.locator('#simple-cargo').inputValue() === '합성 책 상자','Back navigation lost the draft');
        await page.getByRole('button',{name:'다음 · 장소',exact:true}).click();
        await page.getByRole('button',{name:'다음 · 시간',exact:true}).click();
        await page.getByRole('heading',{name:'언제 받을 수 있나요?',exact:true}).waitFor();
        ensure(await page.locator('#simple-pickup-start').inputValue() === '2030-01-02T10:00','Styled numeric Excel date or KST mapping failed');
        await page.getByRole('button',{name:'다음 · 확인',exact:true}).click();
        await page.getByRole('alert').waitFor();
        ensure((await page.getByRole('alert').innerText()).includes('時間') === false,'Foreign UI labels');
        await page.getByRole('checkbox',{name:'표시된 픽업·전달 시간창이 실제 가능한 시각임을 확인했습니다.',exact:true}).check();
        await page.getByRole('button',{name:'다음 · 확인',exact:true}).click();
        await page.getByRole('heading',{name:'보낼 내용을 확인하세요',exact:true}).waitFor();
        await page.screenshot({path:path.join(output,`import-confirm-${width}.png`),fullPage:true});
        report.scenes.push({state:'import-confirm',width,errors:[],horizontalOverflow:false});
        await page.getByRole('button',{name:'배송비 확인하기',exact:true}).click();
        await page.getByRole('heading',{name:'자동 배송 접수 조건',exact:true}).waitFor();
        ensure((await page.locator('[data-input-counters]').innerText()).includes('견적 1 / 등록 0'),'Quote did not remain distinct from create');
        await page.getByRole('button',{name:'이전',exact:true}).click();
        await page.getByRole('button',{name:'이전',exact:true}).click();
        await page.locator('#simple-pickup-address').fill('합성 픽업 장소 수정');
        await page.getByRole('heading',{name:'자동 배송 접수 조건',exact:true}).waitFor({state:'hidden'});
        ensure(await page.getByRole('heading',{name:'자동 배송 접수 조건',exact:true}).count() === 0,'Place change retained the old quote');
        ensure(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),'Interactive import flow overflow');
        ensure(errors.length === 0,`Interactive import errors: ${errors.join(';')}`);
        report.interactions.push({width,check:'Missing fields, template download, empty/formula/oversized rejection, actual XLSX parsing, one-row application, leading zero/date preservation, back navigation, explicit time review, quote invalidation; create count remains zero'});
        await page.close();
      }
      await context.close();
    }
    report.sourceAfter = hashes();
    ensure(JSON.stringify(report.sourceBefore) === JSON.stringify(report.sourceAfter),'Source changed during rendering');
    ensure(report.blockedRequests.length === 0,'Unexpected external or business API requests attempted');
    report.passed = true;
  } finally {
    await browser.close(); fs.writeFileSync(path.join(output,'report.json'),JSON.stringify(report,null,2));
  }
  console.log(JSON.stringify({passed:report.passed,scenes:report.scenes.length,interactions:report.interactions.length,blockedRequests:report.blockedRequests.length}));
})().catch(e => { console.error(e.message); process.exitCode=1; });
