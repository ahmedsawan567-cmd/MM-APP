// إجراءات العملاء المشتركة بين قائمة العملاء وملف العميل
window.MMCustomers = (() => {
  let editing = null, pointsFor = null, notifyFor = null;
  const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  const $ = id => document.getElementById(id);

  async function api(url, method, body) {
    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const data = await res.json().catch(() => ({}));
    if (res.status === 403) throw new Error('ليست لديك صلاحية لهذه العملية.');
    if (!res.ok || data.success === false) throw new Error(data.message || 'تعذّر تنفيذ العملية.');
    return data;
  }

  const reload = (ms = 600) => setTimeout(() => location.reload(), ms);

  function generatePassword() {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
    return Array.from(crypto.getRandomValues(new Uint32Array(10)), b => chars[b % chars.length]).join('');
  }

  function openForm(c) {
    editing = c || null;
    c = c || {};
    $('cf-title').textContent = editing ? 'تعديل بيانات العميل' : 'عميل جديد';
    $('cf-membership-row').classList.toggle('hidden', !editing);
    $('cf-membership').textContent = c.MembershipNumber || '';
    $('cf-name').value = c.FullName || '';
    $('cf-phone').value = c.PhoneNumber || '';
    $('cf-email').value = c.Email || '';
    $('cf-birth').value = c.BirthDate || '';
    $('cf-gender').value = c.Gender || '';
    $('cf-tier').value = c.Tier || $('cf-tier').options[0]?.value || 'BRONZE';
    $('cf-tier-locked').checked = !!c.TierLocked;
    $('cf-points').value = 0;
    $('cf-password').value = '';
    $('cf-points-row').classList.toggle('hidden', !!editing);
    $('cf-password-row').classList.toggle('hidden', !!editing);
    MM.openModal('customer-modal');
  }

  async function save() {
    const v = id => $(id).value.trim();
    const body = {
      fullName: v('cf-name'), phoneNumber: v('cf-phone'), email: v('cf-email') || null,
      birthDate: v('cf-birth') || null, gender: v('cf-gender') || null, tier: v('cf-tier'),
      tierLocked: $('cf-tier-locked').checked
    };
    if (body.fullName.length < 3) return showToast('أدخل الاسم الكامل.', 'warning');
    if (!body.phoneNumber) return showToast('أدخل رقم الهاتف.', 'warning');
    const btn = $('cf-save');
    btn.disabled = true;
    try {
      if (editing) {
        await api(`/api/superadmin/customers/${encodeURIComponent(editing.Id)}`, 'PUT', body);
        showToast('تم حفظ بيانات العميل.', 'success');
        reload(400);
      } else {
        const password = v('cf-password');
        if (password && password.length < 8) return showToast('كلمة المرور 8 أحرف على الأقل.', 'warning');
        const created = await api('/api/superadmin/customers', 'POST', { ...body, initialPoints: Number(v('cf-points')) || 0, password: password || null });
        MM.closeModal('customer-modal');
        await Swal.fire({
          icon: 'success', title: 'تم إنشاء الحساب', confirmButtonText: 'فتح ملف العميل',
          html: `رقم العضوية: <b dir="ltr">${MM.esc(created.membershipNumber)}</b>` +
                (password ? `<br>كلمة مرور التطبيق: <b dir="ltr">${MM.esc(password)}</b><br><small>سلّمها للعميل؛ لن تظهر مرة أخرى.</small>` : '')
        });
        location.href = created.id ? `/AdminDashboard/CustomerDetails/${encodeURIComponent(created.id)}` : location.href;
      }
    } catch (e) {
      showToast(e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  }

  async function resetPassword(c) {
    const ok = await Swal.fire({
      title: 'كلمة مرور جديدة للتطبيق؟', text: `ستُلغى كلمة المرور الحالية لـ ${c.FullName} وتُولَّد كلمة جديدة.`,
      icon: 'question', showCancelButton: true, confirmButtonText: 'توليد كلمة مرور', cancelButtonText: 'إلغاء'
    });
    if (!ok.isConfirmed) return;
    try {
      const d = await api(`/api/superadmin/customers/${encodeURIComponent(c.Id)}/reset-password`, 'POST', {});
      await Swal.fire({
        icon: 'success', title: 'تم ضبط كلمة المرور', confirmButtonText: 'تم',
        html: `العميل يدخل برقم هاتفه <b dir="ltr">${MM.esc(c.PhoneNumber)}</b> وكلمة المرور:<br><br><b dir="ltr" style="font-size:1.25rem">${MM.esc(d.password)}</b><br><br><small>سلّمها للعميل؛ لن تظهر مرة أخرى. يمكنه تغييرها من التطبيق.</small>`
      });
      location.reload();
    } catch (e) {
      showToast(e.message, 'error');
    }
  }

  function openPoints(c) {
    pointsFor = c;
    $('pt-name').textContent = c.FullName;
    $('pt-current').textContent = Number(c.CurrentPoints || 0).toLocaleString();
    $('pt-delta').value = '';
    $('pt-reason').value = '';
    MM.openModal('points-modal');
  }

  async function savePoints() {
    const delta = parseInt($('pt-delta').value, 10);
    const reason = $('pt-reason').value.trim();
    if (!delta) return showToast('أدخل عدد النقاط.', 'warning');
    if (!reason) return showToast('اكتب سبب التعديل.', 'warning');
    try {
      const d = await api(`/api/superadmin/customers/${encodeURIComponent(pointsFor.Id)}/adjust-points`, 'POST', { pointsDelta: delta, reason });
      MM.closeModal('points-modal');
      showToast(`${d.message} الرصيد الجديد: ${d.newBalance}`, 'success');
      reload();
    } catch (e) {
      showToast(e.message, 'error');
    }
  }

  function openNotify(c) {
    notifyFor = c;
    $('nt-name').textContent = c.FullName;
    $('nt-title').value = '';
    $('nt-body').value = '';
    MM.openModal('notify-modal');
  }

  async function sendNotify() {
    const title = $('nt-title').value.trim(), body = $('nt-body').value.trim();
    if (!title || !body) return showToast('اكتب العنوان والنص.', 'warning');
    const btn = $('nt-send');
    btn.disabled = true;
    try {
      const d = await api(`/api/superadmin/customers/${encodeURIComponent(notifyFor.Id)}/notify`, 'POST', { title, body });
      MM.closeModal('notify-modal');
      showToast(d.message, 'success');
      reload(900);
    } catch (e) {
      showToast(e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  }

  async function toggleSuspend(c) {
    const ok = await Swal.fire({
      title: c.IsSuspended ? 'تنشيط الحساب؟' : 'تجميد الحساب؟',
      text: c.IsSuspended ? `سيتمكن ${c.FullName} من استخدام التطبيق مجدداً.` : `لن يتمكن ${c.FullName} من الدخول أو رفع الفواتير حتى تنشّطه.`,
      icon: 'question', showCancelButton: true, confirmButtonText: 'تأكيد', cancelButtonText: 'إلغاء'
    });
    if (!ok.isConfirmed) return;
    try {
      const d = await api(`/api/superadmin/customers/${encodeURIComponent(c.Id)}/toggle-suspend`, 'POST');
      showToast(d.message, 'success');
      reload(500);
    } catch (e) {
      showToast(e.message, 'error');
    }
  }

  async function remove(c, afterUrl) {
    const ok = await Swal.fire({
      title: 'حذف العميل؟',
      html: `${MM.esc(c.FullName)} (<span dir="ltr">${MM.esc(c.MembershipNumber || '')}</span>)<br><br><small>إن كانت له فواتير أو نقاط تُمسح بياناته الشخصية ويُغلق الحساب، وتبقى السجلات المالية بدون هوية. لا يمكن التراجع.</small>`,
      icon: 'warning', showCancelButton: true, confirmButtonText: 'حذف', cancelButtonText: 'إلغاء',
      customClass: { confirmButton: 'btn-danger', cancelButton: 'btn-secondary' }
    });
    if (!ok.isConfirmed) return;
    try {
      const d = await api(`/api/superadmin/customers/${encodeURIComponent(c.Id)}`, 'DELETE');
      showToast(d.message, 'success');
      setTimeout(() => afterUrl ? (location.href = afterUrl) : location.reload(), 700);
    } catch (e) {
      showToast(e.message, 'error');
    }
  }

  return { api, generatePassword, openForm, save, resetPassword, openPoints, savePoints, openNotify, sendNotify, toggleSuspend, remove };
})();
