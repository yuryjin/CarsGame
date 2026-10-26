// Actual .jslib boundary, no browser: denied reads, quota, silent write failures and recovery.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
const source=fs.readFileSync(new URL('../../Assets/Plugins/WebGL/YandexBridge.jslib',import.meta.url),'utf8');
const storage=new Map(), pointers=new Map();let deniedRead=false,deniedWrite=false,discardWrite=false,id=1;
const context={LibraryManager:{library:{}},mergeInto:(a,b)=>Object.assign(a,b),UTF8ToString:x=>x,lengthBytesUTF8:s=>Buffer.byteLength(s),_malloc:()=>id++,stringToUTF8:(s,p)=>pointers.set(p,s),
 localStorage:{getItem:k=>{if(deniedRead)throw new Error('denied');return storage.get(k)??null;},setItem:(k,v)=>{if(deniedWrite)throw new Error('quota');if(!discardWrite)storage.set(k,v);}}};
vm.runInNewContext(source,context);
const api=context.LibraryManager.library,read=()=>JSON.parse(pointers.get(api.CarsLocal_Read('cars.save.v1'))),write=s=>api.CarsLocal_Write('cars.save.v1',s);
assert.deepEqual(read(),{ok:true,found:false,value:''});
assert.equal(write('старый профиль'),1);assert.equal(read().value,'старый профиль');
deniedWrite=true;assert.equal(write('lost'),0);deniedWrite=false;assert.equal(read().value,'старый профиль');
discardWrite=true;assert.equal(write('lost'),0);discardWrite=false;
deniedRead=true;assert.equal(read().ok,false);deniedRead=false;
assert.equal(write('восстановлен 中文'),1);assert.equal(read().value,'восстановлен 中文');
assert.equal(storage.size,1);assert(storage.has('cars.local.cars.save.v1'));
console.log('PASS local-storage bridge: absent/read/write/quota/silent-failure/denied-read/recovery');
