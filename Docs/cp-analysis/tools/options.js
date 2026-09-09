// options.js — Arash wants BOTH:
//   (1) attack speed must matter
//   (2) higher CP must always win
// Model 2 (cadence = R/atkSpd) satisfies (1) but breaks (2). Is there a shape that does both?

const R = 0.6;
const ehp = u => u.hp * (100 + Math.max(0,u.def)) / 100;
const rnd=(a,b)=>a+Math.random()*(b-a);

function evaluate(name, dmgPerHit, cadence, CP, note){
  let n=0, draw=0, ok=0, rev=0, worst=0;
  for(let i=0;i<400000;i++){
    const A={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.9,1.3)};
    const B={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.9,1.3)};
    const ca=CP(A), cb=CP(B); if(ca===cb) continue;
    const ta=Math.ceil(ehp(B)/dmgPerHit(A))*cadence(A);
    const tb=Math.ceil(ehp(A)/dmgPerHit(B))*cadence(B);
    n++;
    if(Math.abs(ta-tb)<1e-12){draw++;continue;}
    const win=ta<tb?'a':'b', says=ca>cb?'a':'b';
    if(win===says) ok++;
    else{rev++; const m=100*Math.abs(ca-cb)/Math.min(ca,cb); if(m>worst)worst=m;}
  }
  const dec=n-draw;
  console.log(`\n${name}`);
  console.log(`  ${note}`);
  console.log(`  draws ${(100*draw/n).toFixed(2)}%   higher CP wins ${(100*ok/dec).toFixed(3)}%   REVERSALS ${rev.toLocaleString()}${rev?`  (worst margin ${worst.toFixed(1)}%)`:''}`);
  return {rev, draws:100*draw/n};
}

console.log('==================================================================');
console.log(' CAN WE HAVE BOTH? attack speed matters AND higher CP always wins');
console.log('==================================================================');

// OPTION 1 - today: attackSpeed does nothing at all
evaluate('OPTION 1  attackSpeed inert (today)',
  A=>A.atk, A=>R, A=>A.atk*ehp(A),
  'cadence flat 0.6s; attackSpeed ignored entirely');

// OPTION 2 - attackSpeed drives cadence (what the report proposed)
evaluate('OPTION 2  attackSpeed drives CADENCE (report proposal)',
  A=>A.atk, A=>R/A.as, A=>A.atk*A.as*ehp(A),
  'faster units swing more often - visually obvious, but breaks the guarantee');

// OPTION 3 - attackSpeed scales DAMAGE PER HIT, cadence stays flat
evaluate('OPTION 3  attackSpeed scales DAMAGE, cadence flat',
  A=>A.atk*A.as, A=>R, A=>A.atk*A.as*ehp(A),
  'same DPS as option 2, but the rounding happens BEFORE a shared cadence');

// OPTION 4 - attackSpeed drives cadence, but cadence is quantised to a common tick
const TICK=0.1;
evaluate('OPTION 4  cadence quantised to a 0.1s tick',
  A=>A.atk, A=>Math.max(TICK,Math.round((R/A.as)/TICK)*TICK), A=>A.atk*A.as*ehp(A),
  'snapping cadence to a grid does not remove the problem');

console.log(`
==================================================================
 WHY OPTION 3 WORKS
==================================================================

 A reversal needs the ROUNDED hit counts to be scaled by DIFFERENT
 numbers. Formally, the winner is decided by

     ceil(EHP_B / dmgA) * cadenceA   vs   ceil(EHP_A / dmgB) * cadenceB

 If cadenceA == cadenceB, the cadence cancels and the comparison is
 purely ceil(x) vs ceil(y). Since x < y implies ceil(x) <= ceil(y),
 the higher-CP unit can only ever TIE - never lose.

 Option 2 puts attackSpeed in the cadence, so the two sides get
 different multipliers and the order can flip after rounding.

 Option 3 puts attackSpeed in the DAMAGE instead. DPS is identical
 (atk * as / R either way), attack speed still matters fully, but the
 cadence stays shared - so the guarantee survives.

 Trade-off: in option 3 a "fast" unit hits HARDER rather than more
 OFTEN. To keep the visual, drive the ATTACK ANIMATION speed from
 attackSpeed while leaving the damage tick shared.
`);
