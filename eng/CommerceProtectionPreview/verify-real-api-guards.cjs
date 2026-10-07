const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const root=path.join(process.cwd(),'artifacts/local/apk-completion-r26');
const tokens=JSON.parse(fs.readFileSync(path.join(root,'server/.private/ui-sessions.json'),'utf8'));
const {caseId,orderNo}=JSON.parse(fs.readFileSync(path.join(root,'real-support-ui/case-summary.json'),'utf8'));
const rows=[];
async function request(role,url,body,expected){
 const token=tokens.accounts.find(x=>x.role===role).accessToken;
 const response=await fetch('http://127.0.0.1:5362/'+url,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+token,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});
 const json=await response.json().catch(()=>({}));rows.push({role,method:body?'POST':'GET',path:url,status:response.status,expected,errorCode:json.code??json.errorCode??null});assert.equal(response.status,expected,url);return json;
}
async function run(){
 const common=`api/v1/common/transaction-disputes/${caseId}`,admin=`api/v1/admin/privacy-support/cases/${caseId}`;
 const initial=await request('orderer',common,undefined,200);
 const fixture=path.join(root,'server/.private/idempotency-fixture.json');
 const command=fs.existsSync(fixture)?JSON.parse(fs.readFileSync(fixture,'utf8')):{clientRequestId:crypto.randomUUID(),expectedRevision:initial.revision,action:'add-evidence',summary:'추가 근거 접수',privateEvidence:'r26 PRIVATE IDEMPOTENCY FIXTURE'};
 if(!fs.existsSync(fixture))fs.writeFileSync(fixture,JSON.stringify(command));
 const accepted=await request('orderer',common+'/commands',command,200);
 const replay=await request('orderer',common+'/commands',command,200);assert.equal(replay.replay,true);assert.equal(replay.revision,accepted.revision);
 await request('orderer',common+'/commands',{...command,privateEvidence:'r26 DIFFERENT FINGERPRINT'},409);
 await request('orderer',common+'/commands',{...command,clientRequestId:crypto.randomUUID()},409);
 await request('restaurant',admin+'/commands',{clientRequestId:crypto.randomUUID(),expectedRevision:accepted.revision,action:'start-review'},403);
 await request('restaurant',common,undefined,200);
 await request('food-driver',common,undefined,404);
 const general=await request('orderer',common,undefined,200);assert.ok(!JSON.stringify(general).includes('PRIVATE IDEMPOTENCY FIXTURE'));
 const final=await request('operator',admin,undefined,200);assert.equal(final.statusCode,'reopened');assert.equal(final.revision,accepted.revision);
 const historyAdds=final.history.filter(x=>x.action==='add-evidence').length;assert.equal(historyAdds,initial.history.filter(x=>x.action==='add-evidence').length+(accepted.replay?0:1));
 const food=await request('orderer',`api/v1/food-orders/${orderNo}`,undefined,200);assert.equal(food.주문.상태,'수령확인');
 const completed=await request('food-driver','api/v1/driver/food-deliveries/settlements/daily?date=2026-10-06',undefined,200);
 const settlement=completed.orderSettlements.find(x=>x.orderNo===orderNo);assert.ok(settlement);assert.equal(settlement.netAmount,null);assert.equal(settlement.isActualTransferCompleted,false);
 return {caseId,orderNo,finalRevision:final.revision,finalStatus:final.statusCode,idempotentReplay:true,changedFingerprint409:true,staleRevision409:true,otherRoleCommands403:true,otherRequesterDetail404:true,generalPrivateTextHidden:true,foodReceiptConfirmed:true,syntheticSettlementOnly:true,actualTransferVerified:false};
}
run().then(summary=>{fs.writeFileSync(path.join(root,'actual-api-guards.json'),JSON.stringify({schemaVersion:'actual-api-guards.r26',status:'Passed',summary,rows,credentialBodiesRecorded:false},null,2));console.log(JSON.stringify({status:'Passed',checks:rows.length,caseId,orderNo}));})
 .catch(e=>{fs.writeFileSync(path.join(root,'actual-api-guards.json'),JSON.stringify({status:'Failed',error:e.message,rows,credentialBodiesRecorded:false},null,2));console.error(e.message);process.exitCode=1;});
