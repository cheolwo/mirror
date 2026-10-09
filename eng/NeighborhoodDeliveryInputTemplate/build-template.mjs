// Blank operational input template. No personal records, formulas, or example shipments.
import fs from 'node:fs/promises';
import path from 'node:path';
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool';

const outputDir = path.resolve(process.argv[2] ?? 'outputs/neighborhood-simple-input-r29');
await fs.mkdir(outputDir, { recursive: true });
const workbook = Workbook.create();
const sheet = workbook.worksheets.add('배송입력');
const guide = workbook.worksheets.add('안내');
const headers = ['행ID(선택)', '물품명', '수량', '중량kg', '픽업주소', '픽업상세주소', '픽업담당자', '픽업전화',
  '전달주소', '전달상세주소', '전달담당자', '전달전화', '픽업시작', '픽업종료', '크기(선택)',
  '전달시작(선택)', '전달종료(선택)', '요청사항(선택)', '차량(선택)'];
sheet.showGridLines = false;
sheet.tabColor = '#21634E';
sheet.getRange('A1:S103').format.font = { name: 'Malgun Gothic', size: 11, color: '#243B35' };
sheet.getRange('A1').values = [['neighborhood-delivery-input.v1']];
sheet.getRange('A1:S1').format.rowHeight = 28;
sheet.getRange('A1:S1').format.fill = '#EAF3EE';
sheet.getRange('A2').values = [['한 행 = 배송 한 건. 4행부터 입력하세요. 업로드 후 한 건을 선택·확인하며 자동 접수되지 않습니다. 상세 규칙은 안내 시트에 있습니다.']];
sheet.getRange('A2:S2').format.rowHeight = 28;
sheet.getRange('A3:S3').values = [headers];
sheet.getRange('A3:S3').format = { fill: '#21634E', font: { name: 'Malgun Gothic', size: 11, bold: true, color: '#FFFFFF' },
  rowHeight: 34, wrapText: true, verticalAlignment: 'center' };
sheet.getRange('A4:S103').format.fill = '#FFFAE9';
sheet.getRange('A4:S103').format.rowHeight = 28;
sheet.getRange('A4:S103').format.borders = { preset: 'inside', style: 'thin', color: '#EBE8DD' };
sheet.getRange('A4:S103').setNumberFormat('@');
sheet.getRange('C4:C103').setNumberFormat('0');
sheet.getRange('D4:D103').setNumberFormat('0.###');
for (const col of ['M', 'N', 'P', 'Q']) sheet.getRange(`${col}4:${col}103`).setNumberFormat('yyyy-mm-dd hh:mm');
for (const [col, width] of [['A', 24], ['B', 24], ['C', 9], ['D', 11], ['E', 34], ['F', 23], ['G', 16], ['H', 19],
  ['I', 34], ['J', 23], ['K', 16], ['L', 19], ['M', 24], ['N', 24], ['O', 16], ['P', 24], ['Q', 24], ['R', 36], ['S', 16]])
  sheet.getRange(`${col}1:${col}103`).format.columnWidth = width;
sheet.freezePanes.freezeRows(3);
sheet.freezePanes.freezeColumns(2);

guide.showGridLines = false;
guide.tabColor = '#B68B31';
guide.getRange('A1:C27').format.font = { name: 'Malgun Gothic', size: 11, color: '#243B35' };
guide.getRange('A1:C1').values = [['생활 배송 엑셀 입력 안내', null, 'r29 / 검토용 양식']];
guide.getRange('A1:C1').format = { fill: '#21634E', font: { name: 'Malgun Gothic', size: 14, bold: true, color: '#FFFFFF' }, rowHeight: 36 };
guide.getRange('A2:C5').values = [
  ['사용 순서', '배송입력 시트의 4행부터 입력 → 앱에서 개인정보 동의 → 엑셀 확인 → 한 행 선택 → 빠진 정보 보완 → 견적·동의·등록', null],
  ['제한', '.xlsx / 파일 2 MiB 이하 / 배송 100행 이하. 수식·매크로·암호·외부 링크를 넣지 않습니다.', null],
  ['안전', '업로드는 미리보기이며 저장·배차·결제를 하지 않습니다. 실제 주소와 연락처는 본인 배송 작성에서만 다룹니다.', null],
  ['중복 주의', '같은 건을 다시 올릴 때 행ID를 유지하세요. 행ID가 없으면 현재 작성 세션 내에서만 재선택 중복을 방지합니다.', null],
];
guide.getRange('A7:C7').values = [['열 / 필수 여부', '입력 규칙', '참고']];
guide.getRange('A7:C7').format = { fill: '#DDEBE4', font: { name: 'Malgun Gothic', size: 11, bold: true }, rowHeight: 30 };
guide.getRange('A8:C26').values = [
  ['행ID / 선택', '이 건의 고정 GUID. 새 건에는 새 ID, 재업로드에는 같은 ID.', '비워 두면 세션 밖 중복 판정 없음'],
  ['물품명 / 필수', '보낼 물품을 간단히 작성합니다.', '임의로 보완하지 않음'],
  ['수량 / 필수', '1 이상의 정수.', '예: 1'],
  ['중량kg / 필수', '0보다 큰 숫자. 단위 kg.', '예: 2.5'],
  ['픽업주소 / 필수', '픽업할 도로명 주소.', '등록 단계에서 기존 주소 검증'],
  ['픽업상세주소 / 선택', '필요한 경우 상세 장소.', '공개 게시물에 포함하지 않음'],
  ['픽업담당자 / 필수', '픽업 현장에서 연락할 사람.', '다른 사람 정보 제공 권한 확인'],
  ['픽업전화 / 필수', '문자 서식 유지. 맨 앞 0을 삭제하지 않습니다.', '자동 숫자 변환에 주의'],
  ['전달주소 / 필수', '받는 장소의 도로명 주소.', '등록 단계에서 기존 주소 검증'],
  ['전달상세주소 / 선택', '필요한 경우 상세 장소.', '공개 게시물에 포함하지 않음'],
  ['전달담당자 / 필수', '받는 현장에서 연락할 사람.', '다른 사람 정보 제공 권한 확인'],
  ['전달전화 / 필수', '문자 서식 유지. 맨 앞 0을 삭제하지 않습니다.', '자동 숫자 변환에 주의'],
  ['픽업시작 / 필수', '한국 시간. 날짜와 시각을 함께 입력합니다.', 'yyyy-mm-dd hh:mm'],
  ['픽업종료 / 필수', '픽업시작보다 뒤인 날짜와 시각.', 'yyyy-mm-dd hh:mm'],
  ['크기 / 선택', 'Small / Medium / Large 중 하나.', '비우면 앱에서 직접 선택'],
  ['전달시작 / 선택', '한국 시간. 전달종료와 함께 입력합니다.', 'yyyy-mm-dd hh:mm'],
  ['전달종료 / 선택', '전달시작보다 뒤인 날짜와 시각.', 'yyyy-mm-dd hh:mm'],
  ['요청사항 / 선택', '현장 전달 시 참고할 짧은 요청.', '동의·요금·확정 상태를 입력하지 않음'],
  ['차량 / 선택', '오토바이 / 다마스 / 라보 / 1톤 중 하나.', '비우면 앱에서 선택'],
];
guide.getRange('A2:C26').format.wrapText = true;
guide.getRange('A2:C26').format.verticalAlignment = 'center';
guide.getRange('A2:A26').format.columnWidth = 26;
guide.getRange('B2:B26').format.columnWidth = 78;
guide.getRange('C2:C26').format.columnWidth = 35;
guide.getRange('A2:C26').format.rowHeight = 42;
guide.getRange('A6:C6').format.rowHeight = 12;
guide.getRange('A8:C26').format.borders = { preset: 'inside', style: 'thin', color: '#E1E8E3' };
guide.freezePanes.freezeRows(7);

workbook.recalculate();
console.log((await workbook.inspect({ kind: 'region', sheetId: '배송입력', range: 'A1:S5', maxChars: 4000, tableMaxCols: 19, tableMaxRows: 5 })).ndjson);
console.log((await workbook.inspect({ kind: 'formula', maxChars: 1000 })).ndjson);
for (const [name, range, file] of [['배송입력', 'A1:I8', 'input-left.png'], ['배송입력', 'J1:S8', 'input-right.png'], ['안내', 'A1:C26', 'guide.png']]) {
  const render = await workbook.render({ sheetName: name, range, scale: 1, format: 'png' });
  await fs.writeFile(path.join(outputDir, file), new Uint8Array(await render.arrayBuffer()));
}
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(path.join(outputDir, '생활배송입력.r29.xlsx'));
console.log(JSON.stringify({ outputDir, schema: 'neighborhood-delivery-input.v1', sheets: ['배송입력', '안내'], reservedRows: 100 }));
