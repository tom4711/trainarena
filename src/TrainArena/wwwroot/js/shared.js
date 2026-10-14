(() => {
  function escapeHtml(text) {
    return String(text ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  /**
   * Apply urgency classes to timer element(s).
   * @param {HTMLElement|null} timerEl
   * @param {number} msLeft
   * @param {boolean} questionOpen
   * @param {HTMLElement[]|null|undefined} extraEls optional siblings (e.g. progress track)
   */
  function setTimerUrgency(timerEl, msLeft, questionOpen, extraEls) {
    const els = [timerEl, ...(extraEls || [])].filter(Boolean);
    for (const el of els) {
      el.classList.remove("is-urgent", "is-critical");
    }
    if (!questionOpen || msLeft <= 0 || els.length === 0) return;
    const sec = Math.ceil(msLeft / 1000);
    const cls = sec <= 5 ? "is-critical" : sec <= 10 ? "is-urgent" : null;
    if (!cls) return;
    for (const el of els) {
      el.classList.add(cls);
    }
  }

  function renderBoardList(boardListEl, entries) {
    if (!boardListEl) return;
    boardListEl.innerHTML = "";
    (entries || []).forEach((e, i) => {
      const li = document.createElement("li");
      li.innerHTML =
        `<span class="rank">${i + 1}.</span>` +
        `<span class="nick"></span>` +
        `<span class="score"></span>`;
      li.querySelector(".nick").textContent = e.nickname;
      li.querySelector(".score").textContent = String(e.score);
      boardListEl.appendChild(li);
    });
  }

  function createFxController({ overlay, titleEl, subEl }) {
    const queue = [];
    let showing = false;
    let timer = null;

    function show(kind, title, sub) {
      queue.push({ kind, title, sub });
      if (!showing) drain();
    }

    function drain() {
      if (!overlay || queue.length === 0) {
        showing = false;
        return;
      }
      showing = true;
      const { kind, title, sub } = queue.shift();
      if (timer) clearTimeout(timer);
      overlay.className = `fx-overlay is-${kind}`;
      if (titleEl) titleEl.textContent = title;
      if (subEl) subEl.textContent = sub || "";
      overlay.classList.remove("hidden");
      const ms = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 1600 : 1100;
      timer = window.setTimeout(() => {
        overlay.classList.add("hidden");
        overlay.className = "fx-overlay hidden";
        drain();
      }, ms);
    }

    return { show };
  }

  window.TrainArenaShared = {
    escapeHtml,
    setTimerUrgency,
    renderBoardList,
    createFxController,
  };
})();
