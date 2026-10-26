import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createContext, runInContext } from 'node:vm';

let registered;
const context = { EQ2Plugins:{ define(id, spec) { assert.equal(id, 'reactive-tracker'); registered = spec; } } };
createContext(context);
runInContext(readFileSync(new URL('../ui/plugin.js', import.meta.url), 'utf8'), context);
assert.ok(registered?.tab && registered?.widget, 'plugin defines its tab and widget');

const empty = registered.widget.render(null);
assert.match(empty, /Waiting for a live Inquisitor or Templar cast/, 'empty overlay explains the next step');
assert.match(empty, /Avacii \(Avacee on GitHub\)/, 'creator credit appears in the overlay');

const state = { selected:'Ipax', characters:['Ipax', 'Avacii'], current:{ character:'Ipax', className:'Inquisitor',
  observation:'Group reactive dropped from Birch.', options:{ coercive:true, eofTwoSet:false, warningCount:1, warningSeconds:5 },
  rows:[{ name:'Ipax', single:{ count:5, seconds:29 }, group:null },
    { name:'Birch', single:null, group:{ count:1, seconds:4 } }] } };
const overlay = registered.widget.render(state);
assert.match(overlay, /data-plugin-change="select"/, 'boxed healers can be selected in the overlay');
assert.match(overlay, /Birch[^]*rt-effect warning[^]*<b>1<\/b>[^]*4s/, 'low count and time are visible as warnings');
assert.match(overlay, /data-plugin-command="source"/, 'overlay credit links to the creator');

const listeners = {};
const root = { addEventListener(name, handler) { listeners[name] = handler; }, innerHTML:'' };
const posts = [];
registered.tab.mount(root, { post:(command, value) => posts.push([command, value]) });
registered.tab.update(root, state);
assert.match(root.innerHTML, /Avacii \(Avacee on GitHub\)/, 'creator credit appears in the tab');
assert.match(root.innerHTML, /protection lines following your cast; those lines do not name the caster/, 'tracking limits are visible');
listeners.change({ target:{ matches:selector => selector === '[data-rt-setting]', dataset:{ rtSetting:'coercive' }, type:'checkbox', checked:false } });
assert.equal(JSON.stringify(posts.at(-1)), JSON.stringify(['setting', { key:'coercive', value:false }]), 'per-healer setting is sent to the plugin');
console.log('Reactives UI checks passed: tab, overlay, warnings, healer selection, credit and settings.');
