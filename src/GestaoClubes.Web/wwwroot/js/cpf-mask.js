(() => {
  const field = document.getElementById('Cpf');
  if (!field) return;
  field.addEventListener('input', () => {
    const raw = field.value;
    const before = raw.slice(0, field.selectionStart ?? raw.length).replace(/[^0-9]/g, '').length;
    const digits = raw.replace(/[^0-9]/g, '').slice(0, 11);
    let formatted = '';
    for (let i = 0; i < digits.length; i++) {
      if (i === 3 || i === 6) formatted += '.';
      if (i === 9) formatted += '-';
      formatted += digits[i];
    }
    if (formatted === raw) return;
    let cursor = 0, seen = 0;
    while (cursor < formatted.length && seen < before) {
      if (/[0-9]/.test(formatted[cursor])) seen++;
      cursor++;
    }
    field.value = formatted;
    field.setSelectionRange(cursor, cursor);
  });
})();
