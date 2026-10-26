(() => {
  const ID = 'reactive-tracker';
  const SOURCE = 'Concept by Avacii (Avacee on GitHub) · ACT ReactiveTracker';
  const esc = value => String(value ?? '').replace(/[&<>"']/g,
    char => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[char]));

  function effect(value, options) {
    if (!value) return '<span class="rt-empty-cell">—</span>';
    const warning = value.count <= (options?.warningCount ?? 1) || value.seconds <= (options?.warningSeconds ?? 5);
    return `<span class="rt-effect${warning ? ' warning' : ''}" aria-label="${value.count} triggers, ${value.seconds} seconds remaining"><b>${value.count}</b><span>${value.seconds}s</span></span>`;
  }

  function rows(current) {
    const options = current?.options;
    return (current?.rows || []).map(row => `<div class="rt-person">
      <strong title="${esc(row.name)}">${esc(row.name)}</strong>
      <span>${effect(row.single, options)}</span><span>${effect(row.group, options)}</span>
    </div>`).join('');
  }

  function picker(state, widget = false) {
    const names = state?.characters || [];
    if (names.length < 2) return state?.selected ? `<strong class="rt-healer">${esc(state.selected)}</strong>` : '';
    return `<select class="rt-select" aria-label="Healer" ${widget ? 'data-plugin-change="select"' : 'data-rt-select'}>
      ${names.map(name => `<option value="${esc(name)}"${name === state.selected ? ' selected' : ''}>${esc(name)}</option>`).join('')}</select>`;
  }

  function table(current) {
    const body = rows(current);
    return `<div class="rt-table"><div class="rt-table-head"><span>Group member</span><span>Single</span><span>Group</span></div>
      ${body || `<p class="rt-empty">${current ? 'No active reactives.' : 'Waiting for a live Inquisitor or Templar cast.'}</p>`}</div>`;
  }

  function tabHtml(state) {
    const current = state?.current;
    const options = current?.options || {};
    return `<div class="rt-page"><header class="rt-page-head"><div><span class="rt-eyebrow">LIVE HEALING</span><h2>Reactives</h2>
      <p>Remaining reactive triggers and time for each group member.</p></div><div class="rt-tools">${picker(state)}
      ${current ? '<button type="button" class="rt-btn" data-rt-clear>Clear</button>' : ''}</div></header>
      <div class="rt-content"><section class="rt-card"><div class="rt-card-head"><h3>${current ? `${esc(current.className || 'Healer')} · ${esc(current.character)}` : 'Waiting for your healer'}</h3><span>Live log only</span></div>
        ${table(current)}${current?.observation ? `<p class="rt-observation">${esc(current.observation)}</p>` : ''}
      </section>
      <section class="rt-card rt-settings"><h3>Tracking</h3>
        <p>These bonuses apply to new casts. Existing counts stay as observed.</p>
        <label><span>Coercive Healing <small>+1 single and group trigger</small></span><input type="checkbox" data-rt-setting="coercive"${options.coercive ? ' checked' : ''}${current ? '' : ' disabled'}></label>
        <label><span>Templar EoF 2-piece <small>+4 single triggers</small></span><input type="checkbox" data-rt-setting="eofTwoSet"${options.eofTwoSet ? ' checked' : ''}${current ? '' : ' disabled'}></label>
        <label><span>Warn at or below <small>Triggers remaining</small></span><input type="number" min="0" max="10" data-rt-setting="warningCount" value="${options.warningCount ?? 1}"${current ? '' : ' disabled'}></label>
        <label><span>Warn within <small>Seconds remaining</small></span><input type="number" min="0" max="30" data-rt-setting="warningSeconds" value="${options.warningSeconds ?? 5}"${current ? '' : ' disabled'}></label>
        <p class="rt-note">Group members are inferred from protection lines following your cast; those lines do not name the caster. An unseen cast cannot give a reliable remaining count.</p>
      </section></div>
      <button type="button" class="rt-credit" data-rt-source>${SOURCE}</button></div>`;
  }

  function mount(root, ctx) {
    root._rt = { state:null, ctx };
    root.addEventListener('change', event => {
      const target = event.target;
      if (target.matches('[data-rt-select]')) ctx.post('select', { character:target.value });
      else if (target.matches('[data-rt-setting]')) ctx.post('setting', { key:target.dataset.rtSetting,
        value:target.type === 'checkbox' ? target.checked : Number(target.value) });
    });
    root.addEventListener('click', event => {
      if (event.target.closest('[data-rt-clear]')) ctx.post('clear', {});
      else if (event.target.closest('[data-rt-source]')) ctx.post('source', {});
    });
  }

  function widgetHtml(state) {
    const current = state?.current;
    return `<div class="rt-widget"><div class="rt-widget-head"><strong>Reactives</strong>${picker(state, true)}</div>
      ${table(current)}${current?.observation && !current.rows?.length ? `<p class="rt-observation">${esc(current.observation)}</p>` : ''}
      <button type="button" class="rt-credit" data-plugin-command="source">${SOURCE}</button></div>`;
  }

  EQ2Plugins.define(ID, {
    tab:{ mount, update(root, state) {
      if (!root._rt) return;
      root._rt.state = state;
      // A live state push can arrive while a warning threshold is being typed.
      if (root.querySelector?.(':focus')?.matches('[data-rt-setting][type="number"]')) return;
      root.innerHTML = tabHtml(state);
    } },
    widget:{ render:widgetHtml },
  });
})();
