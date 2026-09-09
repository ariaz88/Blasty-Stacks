// robust.js — does the tuned SUM (wA 1, wH 0.35, wD 0.9) generalise, or is it fitted
// to the current roster? Tests both metrics on a wide sweep of synthetic builds.
const dmg = (a,d) => Math.max(1, a*100/(100+Math.max(0,d)));
const htk = (hp,per) => Math.ceil(hp/per);
const ehp = s => s.hp * (100 + Math.max(0,s.def)) / 100;

const SUM  = s => 1.0*s.atk + 0.35*s.hp + 0.9*s.def;
const PROD = s => s.atk * ehp(s);

function trial(units, label) {
  let n=0, sumOK=0, prodOK=0;
  const sumFails=[];
  for (let i=0;i<units.length;i++) for (let j=0;j<units.length;j++) {
    if (i===j) continue;
    const A=units[i], B=units[j];
    const aH=htk(B.hp,dmg(A.atk,B.def)), bH=htk(A.hp,dmg(B.atk,A.def));
    if (aH===bH) continue;
    const win = aH<bH ? 'a':'b';
    n++;
    const sa=SUM(A), sb=SUM(B);
    if (sa!==sb && (sa>sb?'a':'b')===win) sumOK++; else if (sa!==sb) sumFails.push([A,B,win]);
    const pa=PROD(A), pb=PROD(B);
    if ((pa>pb?'a':'b')===win) prodOK++;
  }
  console.log(`${label}`);
  console.log(`   duels: ${n}`);
  console.log(`   tuned SUM  (1, 0.35, 0.9) : ${(100*sumOK/n).toFixed(1)}%`);
  console.log(`   PRODUCT ATK x EffectiveHP : ${(100*prodOK/n).toFixed(1)}%`);
  if (sumFails.length) {
    const [A,B,win] = sumFails[0];
    console.log(`   e.g. SUM wrong: A(atk ${A.atk} hp ${A.hp} def ${A.def}) vs B(atk ${B.atk} hp ${B.hp} def ${B.def}) -> ${win} wins`);
  }
  console.log('');
}

// 1) roughly the current roster's value ranges
const near = [];
for (const atk of [35,50,64,72,90,120,200])
  for (const hp of [100,150,205,275,340,500])
    for (const def of [25,35,52,65,78])
      near.push({atk,hp,def});
trial(near, '--- A) within the current roster ranges ---');

// 2) wider: includes glass cannons and heavy tanks
const wide = [];
for (const atk of [10,30,64,120,250,500])
  for (const hp of [50,100,300,800,2000])
    for (const def of [0,10,25,60,120,250,400])
      wide.push({atk,hp,def});
trial(wide, '--- B) wide range (glass cannons + heavy tanks) ---');

// 3) high-defense regime specifically - where a linear DEF term should break down
const tanky = [];
for (const atk of [40,80,160])
  for (const hp of [200,600,1500])
    for (const def of [100,200,300,500,800])
      tanky.push({atk,hp,def});
trial(tanky, '--- C) high-defense regime (DEF 100-800) ---');

console.log('WHY: the sum uses a LINEAR def term (0.9*def), but real survivability');
console.log('scales as HP*(100+DEF)/100 - i.e. DEF multiplies HP. The two agree only');
console.log('while HP and DEF stay inside a narrow band. Outside it they diverge.');
