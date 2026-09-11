/**
 * The `#do=` step vocabulary shared by shoot.mjs (stills) and record.mjs
 * (clips): one step → one JavaScript expression evaluated in the page, which
 * returns a short line to log or a string starting with `NOT FOUND` when the
 * control is not there — the caller then aborts rather than capture a frame of
 * the wrong thing.
 *
 *   <label>[@n]      click the nth <button> whose text starts with <label>
 *   row:<text>       expand the log row that CONTAINS <text>
 *   select:<value>   pick an <option> by value, through React's setter
 *   fill:<text>      type into the first textarea, else an open dialog's input
 */
export function stepExpression(step) {
  if (step.startsWith("row:")) {
    return `(() => {
      const needle = ${JSON.stringify(step.slice(4))};
      const li = [...document.querySelectorAll('li')]
        .find((n) => (n.textContent || '').includes(needle));
      if (!li) return 'NOT FOUND: a row containing ' + needle;
      const btn = [...li.querySelectorAll('button')]
        .find((b) => /^(Timeline|Hide|履歴|閉じる)/.test((b.textContent || '').trim()));
      if (!btn) return 'NOT FOUND: an expander in the row for ' + needle;
      btn.click();
      return 'expanded row: ' + needle;
    })()`;
  }
  if (step.startsWith("select:")) {
    return `(() => {
      // select:<value> — pick an <option> by value in the first <select>
      // that has it, through React's own setter so onChange fires.
      const value = ${JSON.stringify(step.slice(7))};
      const sel = [...document.querySelectorAll('select')]
        .find((s) => [...s.options].some((o) => o.value === value));
      if (!sel) return 'NOT FOUND: a select with option ' + value;
      const setter = Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype, 'value').set;
      setter.call(sel, value);
      sel.dispatchEvent(new Event('change', { bubbles: true }));
      return 'selected: ' + value;
    })()`;
  }
  if (step.startsWith("fill:")) {
    return `(() => {
      const text = ${JSON.stringify(step.slice(5))};
      // A textarea when the page has one, else the text input inside an
      // open dialog — the Rotations workbench's typed confirmations
      // ("type PUBLISH", "type wk_2026_38") are inputs, not textareas.
      const ta = document.querySelector('textarea')
        ?? document.querySelector('[role="dialog"] input[type="text"]');
      if (!ta) return 'NOT FOUND: a textarea or a dialog text input';
      // React tracks the last value it set on the node; assigning .value
      // directly leaves that tracker in step and the change is swallowed.
      const setter = Object.getOwnPropertyDescriptor(
        ta.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype,
        'value').set;
      setter.call(ta, text);
      ta.dispatchEvent(new Event('input', { bubbles: true }));
      return 'filled: ' + text.slice(0, 40);
    })()`;
  }
  return `(() => {
    const raw = ${JSON.stringify(step)};
    const at = /@(\\d+)$/.exec(raw);
    const wanted = at ? raw.slice(0, at.index) : raw;
    const nth = at ? Number(at[1]) : 0;
    const all = [...document.querySelectorAll('button')]
      .filter((b) => (b.textContent || '').trim().startsWith(wanted));
    const el = all[nth];
    if (!el) return 'NOT FOUND: ' + wanted + ' #' + nth + ' (' + all.length + ' matched)';
    el.click();
    return 'clicked: ' + (el.textContent || '').trim().slice(0, 40);
  })()`;
}
