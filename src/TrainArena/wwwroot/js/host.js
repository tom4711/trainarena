(() => {
  const $ = (id) => document.getElementById(id);
  const setup = $("setup");
  const live = $("live");
  const btnCreate = $("btn-create");
  const btnStart = $("btn-start");
  const btnNext = $("btn-next");
  const quizSelect = $("quiz-select");
  const roomCodeEl = $("room-code");
  const lobbyStatus = $("lobby-status");
  const playerList = $("player-list");
  const progressEl = $("progress");
  const questionText = $("question-text");
  const questionImage = $("question-image");
  const optionsEl = $("options");
  const timerEl = $("timer");
  const revealEl = $("reveal");
  const board = $("board");
  const boardList = $("board-list");
  const boardHint = $("board-hint");
  const boardAutoAdvance = $("board-auto-advance");
  const finishedEl = $("finished");
  const reviewEl = $("review");
  const reviewList = $("review-list");
  const qrWrap = $("qr-wrap");
  const qrCanvas = $("qr-canvas");
  const joinUrlEl = $("join-url");
  const arenaActions = $("arena-actions");
  const btnBoost = $("btn-boost");
  const btnTimePlus = $("btn-time-plus");
  const arenaStatus = $("arena-status");
  const fxOverlay = $("fx-overlay");
  const fxTitle = $("fx-title");
  const fxSub = $("fx-sub");

  const STORAGE_ROOM = "trainarena.host.room";
  const shared = window.TrainArenaShared;
  const fx = shared.createFxController({
    overlay: fxOverlay,
    titleEl: fxTitle,
    subEl: fxSub,
  });
  const escapeHtml = shared.escapeHtml;

  let startedAt = null;
  let endsAt = null;
  let timerHandle = null;
  let questionOpen = false;
  let hostArenaUsed = false;
  let autoAdvanceHandle = null;
  let roomCode = sessionStorage.getItem(STORAGE_ROOM) || "";
  let hostAsyncRace = false;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  function disableLobbySetup() {
    $("powerup-setup").querySelectorAll("input").forEach((el) => {
      el.disabled = true;
    });
    $("auto-advance-setup").querySelectorAll("input, select").forEach((el) => {
      el.disabled = true;
    });
    const exam = $("exam-setup");
    if (exam) {
      exam.querySelectorAll("input").forEach((el) => {
        el.disabled = true;
      });
    }
    const asyncRace = $("async-race-setup");
    if (asyncRace) {
      asyncRace.querySelectorAll("input").forEach((el) => {
        el.disabled = true;
      });
    }
  }

  function readAutoAdvanceConfig() {
    const delay = parseInt($("aa-delay").value, 10);
    return {
      enabled: $("aa-enabled").checked,
      delaySeconds: [3, 5, 10].includes(delay) ? delay : 5,
    };
  }

  function readPowerUpConfig() {
    const num = (id) => {
      const v = parseInt($(id).value, 10);
      return Number.isFinite(v) ? Math.max(0, v) : 0;
    };
    return {
      enabled: $("pu-enabled").checked,
      starter: {
        fifty_fifty: num("pu-fifty"),
        double: num("pu-double"),
        extra_time: num("pu-extra"),
        shield: num("pu-shield"),
        disrupt: num("pu-disrupt"),
      },
      streakRewardEvery: num("pu-streak"),
      maxStackPerType: 3,
      hostEventsEnabled: true,
      maxHostEventPerQuestion: 1,
    };
  }

  function setArenaVisible(visible) {
    arenaActions.classList.toggle("hidden", !visible);
    if (!visible) {
      arenaStatus.classList.add("hidden");
      arenaStatus.textContent = "";
    }
  }

  function setArenaButtonsEnabled(enabled) {
    btnBoost.disabled = !enabled;
    btnTimePlus.disabled = !enabled;
  }

  function updateEndsAtFromServer(endsAtUtc) {
    if (!endsAtUtc) return;
    endsAt = new Date(endsAtUtc);
    if (questionOpen) startTimer();
  }

  function setLobbyStatus(text, kind) {
    lobbyStatus.textContent = text;
    lobbyStatus.classList.remove("is-loading", "is-error", "is-ok");
    if (kind) lobbyStatus.classList.add(kind);
  }

  function clearReview() {
    reviewEl.classList.add("hidden");
    reviewList.innerHTML = "";
  }

  function renderReview(items) {
    reviewList.innerHTML = "";
    if (!items || !items.length) {
      reviewEl.classList.add("hidden");
      return;
    }
    items.forEach((r) => {
      const pct =
        r.answeredCount > 0
          ? Math.round((100 * r.correctCount) / r.answeredCount) + " %"
          : "—";
      const block = document.createElement("article");
      block.className = "review-item";
      const opts = (r.options || [])
        .map((opt, i) => {
          const count = (r.counts && r.counts[i]) || 0;
          const correct = i === r.correctIndex ? "is-correct" : "";
          return `<li class="${correct}"><span class="opt-index">${i + 1}</span> ${escapeHtml(opt)} <span class="muted">× ${count}</span></li>`;
        })
        .join("");
      const tags = (r.tags || []).map((t) => `<span class="tag-chip">${escapeHtml(t)}</span>`).join("");
      const expl = r.explanation
        ? `<div class="explanation"><strong>Erklärung:</strong> <span class="explanation-body"></span></div>`
        : "";
      const ihkPts = r.ihkPoints ? `<span class="muted"> · ${r.ihkPoints} IHK-Pkte</span>` : "";
      block.innerHTML = `
      <h4><span class="review-num">${r.index + 1}.</span> <span class="review-qtext"></span></h4>
      <p class="muted">${pct} richtig · ${r.answeredCount} von ${r.playerCount} geantwortet${ihkPts}</p>
      <div class="tag-row">${tags}</div>
      <ol class="options review-options">${opts}</ol>
      ${expl}`;
      const qEl = block.querySelector(".review-qtext");
      if (window.TrainArenaQuestionFormat) {
        window.TrainArenaQuestionFormat.renderQuestionText(qEl, r.text);
      } else if (qEl) {
        qEl.textContent = r.text || "";
      }
      const explBody = block.querySelector(".explanation-body");
      if (explBody && r.explanation) {
        if (window.TrainArenaQuestionFormat) {
          window.TrainArenaQuestionFormat.renderQuestionText(explBody, r.explanation);
        } else {
          explBody.textContent = r.explanation;
        }
      }
      reviewList.appendChild(block);
    });
    reviewEl.classList.remove("hidden");
  }

  function renderIhkResults(results) {
    let box = $("ihk-results");
    if (!box) {
      box = document.createElement("div");
      box.id = "ihk-results";
      box.className = "ihk-results";
      finishedEl.insertAdjacentElement("afterend", box);
    }
    if (!results || !results.length) {
      box.classList.add("hidden");
      box.innerHTML = "";
      return;
    }
    box.classList.remove("hidden");
    box.innerHTML =
      "<h3>IHK-Ergebnis</h3><ol>" +
      results
        .map(
          (r) =>
            `<li><strong></strong> — ${r.points}/100 · Note ${r.grade} · ${
              r.passed ? "bestanden" : "nicht bestanden"
            }</li>`,
        )
        .join("") +
      "</ol>";
    [...box.querySelectorAll("li strong")].forEach((el, i) => {
      el.textContent = results[i].nickname;
    });
  }

  async function loadQuizTags(quizId) {
    const wrap = $("tag-filter-wrap");
    const list = $("tag-filter");
    if (!wrap || !list) return;
    list.innerHTML = "";
    if (!quizId) {
      wrap.classList.add("hidden");
      return;
    }
    try {
      const res = await fetch(`/api/quizzes/${quizId}/tags`);
      if (!res.ok) throw new Error("tags");
      const data = await res.json();
      const tags = data.tags || [];
      if (!tags.length) {
        wrap.classList.add("hidden");
        return;
      }
      wrap.classList.remove("hidden");
      tags.forEach((tag) => {
        const label = document.createElement("label");
        label.className = "inline tag-option";
        label.innerHTML = `<input type="checkbox" data-tag="" /> <span></span>`;
        label.querySelector("input").dataset.tag = tag;
        label.querySelector("span").textContent = tag;
        list.appendChild(label);
      });
    } catch {
      wrap.classList.add("hidden");
    }
  }

  function readExamConfig() {
    const enabled = !!$("exam-enabled")?.checked;
    const tags = [...document.querySelectorAll("#tag-filter input[data-tag]:checked")].map(
      (el) => el.dataset.tag,
    );
    return { enabled, tags };
  }

  function readAsyncRaceConfig() {
    return { enabled: !!$("async-race-enabled")?.checked };
  }

  function updateStartButtonLabel() {
    if (!btnStart || btnStart.classList.contains("hidden")) return;
    btnStart.textContent = readAsyncRaceConfig().enabled ? "Rennen starten" : "Quiz starten";
  }

  function syncAsyncRaceUi() {
    const asyncCb = $("async-race-enabled");
    const asyncOn = !!asyncCb?.checked;
    const examOn = !!$("exam-enabled")?.checked;
    const aaOn = !!$("aa-enabled")?.checked;
    const blockExamAa = asyncOn;
    const blockAsync = examOn || aaOn;

    if (asyncCb) asyncCb.disabled = blockAsync;
    $("exam-setup")?.querySelectorAll("input").forEach((el) => {
      el.disabled = blockExamAa;
    });
    $("auto-advance-setup")?.querySelectorAll("input, select").forEach((el) => {
      el.disabled = blockExamAa;
    });

    if (blockExamAa) {
      const pu = $("powerup-setup");
      if (pu) {
        pu.classList.remove("is-disabled");
        pu.querySelectorAll("input").forEach((el) => {
          el.disabled = false;
        });
      }
    } else {
      syncExamUi();
    }
    updateStartButtonLabel();
  }

  function setAsyncRaceLiveView(active) {
    const hideIds = [
      "question-text",
      "question-image",
      "options",
      "timer",
      "reveal",
      "question-explanation",
      "arena-actions",
      "board",
      "btn-next",
    ];
    hideIds.forEach((id) => {
      const el = $(id);
      if (el) el.classList.toggle("hidden", active);
    });
    $("async-scoreboard")?.classList.toggle("hidden", !active);
  }

  function renderAsyncScoreboard(msg) {
    hostAsyncRace = true;
    clearAutoAdvanceCountdown();
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    btnStart.classList.add("hidden");
    setArenaVisible(false);
    revealEl.classList.add("hidden");
    finishedEl.classList.add("hidden");
    clearReview();
    renderIhkResults(null);
    setAsyncRaceLiveView(true);
    progressEl.textContent = "Async-Rennen läuft";

    const list = $("async-scoreboard-list");
    list.innerHTML = "";
    (msg.entries || []).forEach((e, i) => {
      const li = document.createElement("li");
      li.innerHTML = `<span class="rank">${i + 1}.</span><span class="race-line"></span>`;
      li.querySelector(".race-line").textContent =
        `${e.nickname} — ${e.score} — ${e.answeredCount}/${e.total}`;
      list.appendChild(li);
    });
  }

  function syncExamUi() {
    const on = !!$("exam-enabled")?.checked;
    const pu = $("powerup-setup");
    if (pu) {
      pu.classList.toggle("is-disabled", on);
      pu.querySelectorAll("input").forEach((el) => {
        el.disabled = on;
      });
    }
  }

  function renderBoardEntries(entries) {
    shared.renderBoardList(boardList, entries);
  }

  function setTimerUrgency(msLeft) {
    shared.setTimerUrgency(timerEl, msLeft, questionOpen);
  }

  async function loadQuizzes() {
    quizSelect.disabled = true;
    quizSelect.innerHTML = `<option value="">Quiz werden geladen…</option>`;
    btnCreate.disabled = true;
    try {
      const res = await fetch("/api/quizzes");
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const quizzes = await res.json();
      quizSelect.innerHTML = "";
      quizzes.forEach((q) => {
        const opt = document.createElement("option");
        opt.value = q.id;
        opt.textContent = `${q.title} (${q.questionCount} Fragen)`;
        quizSelect.appendChild(opt);
      });
      if (quizzes.length === 0) {
        quizSelect.innerHTML = `<option value="">Kein Quiz vorhanden</option>`;
        setLobbyStatus("Kein Quiz vorhanden — zuerst im Editor anlegen.", "is-error");
        btnCreate.disabled = true;
        return;
      }
      quizSelect.disabled = false;
      btnCreate.disabled = false;
      await loadQuizTags(quizSelect.value);
      syncAsyncRaceUi();
    } catch (err) {
      quizSelect.innerHTML = `<option value="">Laden fehlgeschlagen</option>`;
      setLobbyStatus(`Quiz-Liste fehlgeschlagen: ${err}`, "is-error");
      btnCreate.disabled = true;
      throw err;
    }
  }

  function showJoinArtifacts(code) {
    roomCode = code;
    sessionStorage.setItem(STORAGE_ROOM, code);
    roomCodeEl.textContent = code;
    roomCodeEl.classList.remove("hidden");
    const url = `${location.origin}/player/?code=${encodeURIComponent(code)}`;
    joinUrlEl.innerHTML = `Beitrittslink: <a href="${url}">${url}</a>`;
    qrWrap.classList.remove("hidden");
    if (!window.QRCode) {
      console.error("QRCode-Bibliothek nicht geladen — QR-Anzeige nicht möglich.");
      return;
    }
    const ctx = qrCanvas.getContext("2d");
    if (ctx) {
      ctx.clearRect(0, 0, qrCanvas.width, qrCanvas.height);
    }
    QRCode.toCanvas(qrCanvas, url, { width: 180, margin: 1 }, (err) => {
      if (err) console.error(err);
    });
  }

  connection.on("RoomCreated", (msg) => {
    clearReview();
    showJoinArtifacts(msg.code);
    btnCreate.disabled = true;
    quizSelect.disabled = true;
    disableLobbySetup();
    btnStart.classList.remove("hidden");
    updateStartButtonLabel();
    setLobbyStatus("0 Spieler verbunden — warte auf Beitritte…");
  });

  connection.on("LobbyState", (msg) => {
    playerList.innerHTML = "";
    (msg.players || []).forEach((p) => {
      const li = document.createElement("li");
      li.textContent = p.nickname + (p.isConnected === false ? " (getrennt)" : "");
      if (p.isConnected === false) li.classList.add("offline");
      playerList.appendChild(li);
    });
    const count = msg.connectedCount ?? 0;
    setLobbyStatus(
      count === 0 ? "0 Spieler verbunden — warte auf Beitritte…" : `${count} Spieler verbunden`,
      count > 0 ? "is-ok" : null,
    );
    btnStart.disabled = count < 1;
  });

  connection.on("JoinError", (msg) => {
    setLobbyStatus(msg.error || "Fehler", "is-error");
    if (!roomCode && !quizSelect.disabled) {
      btnCreate.disabled = false;
    }
    if (!live.classList.contains("hidden")) {
      btnStart.disabled = false;
    }
  });

  function clearAutoAdvanceCountdown() {
    if (autoAdvanceHandle) clearInterval(autoAdvanceHandle);
    autoAdvanceHandle = null;
    boardAutoAdvance.classList.add("hidden");
    boardAutoAdvance.textContent = "";
  }

  function startAutoAdvanceCountdown(advancesAtUtc) {
    clearAutoAdvanceCountdown();
    boardHint.classList.add("hidden");
    boardAutoAdvance.classList.remove("hidden");
    const target = new Date(advancesAtUtc);
    const tick = () => {
      const sec = Math.max(0, Math.ceil((target - Date.now()) / 1000));
      boardAutoAdvance.textContent =
        sec > 0 ? `Automatisch in ${sec}s` : "Automatisch gleich…";
    };
    tick();
    autoAdvanceHandle = setInterval(tick, 200);
  }

  connection.on("QuestionStarted", (msg) => {
    if (hostAsyncRace) return;
    clearAutoAdvanceCountdown();
    setAsyncRaceLiveView(false);
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    revealEl.classList.add("hidden");
    const explEl = $("question-explanation");
    if (explEl) {
      explEl.classList.add("hidden");
      explEl.textContent = "";
    }
    board.classList.add("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.add("hidden");
    finishedEl.textContent = "Runde beendet. Danke!";
    renderIhkResults(null);
    clearReview();
    questionOpen = true;
    hostArenaUsed = false;
    btnBoost.classList.remove("is-spent", "is-activating");
    btnTimePlus.classList.remove("is-spent", "is-activating");
    arenaStatus.classList.remove("is-boost", "is-time", "is-pu-toast");
    setArenaVisible(true);
    setArenaButtonsEnabled(true);
    progressEl.textContent = `Frage ${(msg.index ?? 0) + 1} von ${msg.totalQuestions ?? "?"}`;
    if (window.TrainArenaQuestionFormat) {
      window.TrainArenaQuestionFormat.renderQuestionText(questionText, msg.text);
    } else {
      questionText.textContent = msg.text;
    }
    if (msg.imageUrl) {
      questionImage.src = msg.imageUrl;
      questionImage.classList.remove("hidden");
    } else {
      questionImage.removeAttribute("src");
      questionImage.classList.add("hidden");
    }
    optionsEl.innerHTML = "";
    (msg.options || []).forEach((opt, i) => {
      const li = document.createElement("li");
      li.dataset.index = String(i);
      li.innerHTML = `<span class="opt-index">${i + 1}</span><span class="opt-text"></span>`;
      li.querySelector(".opt-text").textContent = opt;
      optionsEl.appendChild(li);
    });
    startedAt = new Date(msg.startedAtUtc);
    endsAt = new Date(msg.endsAtUtc);
    startTimer();
  });

  connection.on("QuestionEnded", (msg) => {
    questionOpen = false;
    setArenaVisible(false);
    stopTimer();
    timerEl.textContent = "0s";
    timerEl.classList.remove("is-urgent", "is-critical");
    [...optionsEl.querySelectorAll("li")].forEach((li) => {
      li.classList.toggle("is-correct", Number(li.dataset.index) === msg.correctIndex);
    });
    revealEl.textContent = `Richtige Antwort: Option ${msg.correctIndex + 1}`;
    revealEl.classList.remove("hidden");
    const explEl = $("question-explanation");
    if (explEl) {
      if (msg.explanation) {
        explEl.classList.remove("hidden");
        explEl.innerHTML = `<strong>Erklärung:</strong> <span class="explanation-body"></span>`;
        const body = explEl.querySelector(".explanation-body");
        if (window.TrainArenaQuestionFormat) {
          window.TrainArenaQuestionFormat.renderQuestionText(body, msg.explanation);
        } else if (body) {
          body.textContent = msg.explanation;
        }
      } else {
        explEl.classList.add("hidden");
        explEl.textContent = "";
      }
    }
  });

  connection.on("Leaderboard", (msg) => {
    if (hostAsyncRace) return;
    clearAutoAdvanceCountdown();
    setArenaVisible(false);
    renderBoardEntries(msg.entries);
    board.classList.remove("hidden");
    boardHint.classList.remove("hidden");
    btnNext.classList.remove("hidden");
    btnNext.textContent = msg.hasMoreQuestions ? "Nächste Frage" : "Abschluss zeigen";
  });

  connection.on("AsyncScoreboard", renderAsyncScoreboard);

  connection.on("AutoAdvanceScheduled", (msg) => {
    if (msg?.advancesAtUtc) startAutoAdvanceCountdown(msg.advancesAtUtc);
  });

  connection.on("AutoAdvanceCancelled", () => {
    clearAutoAdvanceCountdown();
    if (!board.classList.contains("hidden")) {
      boardHint.classList.remove("hidden");
    }
  });

  connection.on("GameFinished", (msg) => {
    clearAutoAdvanceCountdown();
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    btnStart.classList.add("hidden");
    setArenaVisible(false);
    if (hostAsyncRace) {
      setAsyncRaceLiveView(false);
    }
    renderBoardEntries(msg.entries);
    board.classList.remove("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.remove("hidden");
    progressEl.textContent =
      msg.examMode ? "Prüfung beendet" : hostAsyncRace ? "Rennen beendet" : "Finale";
    if (msg.examMode) {
      finishedEl.textContent = "Prüfung beendet — IHK-Bewertung unten.";
    }
    renderIhkResults(msg.examMode ? msg.ihkResults : null);
    renderReview(msg.review);
  });

  function flashClass(el, className, ms = 420) {
    if (!el) return;
    el.classList.remove(className);
    void el.offsetWidth;
    el.classList.add(className);
    window.setTimeout(() => el.classList.remove(className), ms);
  }

  function bumpTimer() {
    flashClass(timerEl, "is-extended", 560);
  }

  function showArenaToast(text, kind) {
    arenaStatus.textContent = text;
    arenaStatus.classList.remove("hidden", "is-boost", "is-time", "is-pu-toast");
    void arenaStatus.offsetWidth;
    arenaStatus.classList.add("is-pu-toast");
    if (kind) arenaStatus.classList.add(kind);
  }

  function showFx(kind, title, sub) {
    fx.show(kind, title, sub);
  }

  connection.on("PowerUpFx", (msg) => {
    const kind = msg?.kind;
    const actor = msg?.actorNickname || "?";
    const target = msg?.targetNickname || "?";
    if (kind === "shield_up") {
      showFx(kind, "Schild aktiv", `${actor} ist geschützt`);
      showArenaToast(`Schild aktiv: ${actor}`, "is-time");
    } else if (kind === "shield_break") {
      showFx(kind, "Schild zerstört", `${actor} hält den Angriff von ${target} nicht`);
      showArenaToast(`Schild zerstört: ${actor}`, "is-boost");
    } else if (kind === "attack_launch") {
      showFx(kind, "Störimpuls!", `${actor} greift ${target} an`);
    } else if (kind === "attack_hit") {
      showFx(kind, "Treffer!", `${target} ist gestört — nächste Antwort zählt nicht`);
      showArenaToast(`Störimpuls trifft ${target}`, "is-boost");
    } else if (kind === "attack_blocked") {
      showFx(kind, "Geblockt!", `${target} blockt ${actor} mit dem Schild`);
      showArenaToast(`Schild blockt Angriff auf ${target}`, "is-time");
    }
  });

  connection.on("ArenaEvent", (msg) => {
    updateEndsAtFromServer(msg.endsAtUtc);
    const btn =
      msg.powerUpId === "boost_all"
        ? btnBoost
        : msg.powerUpId === "time_plus"
          ? btnTimePlus
          : null;
    flashClass(btn, "is-activating", 400);
    if (msg.powerUpId === "boost_all") {
      showArenaToast("Team-Boost aktiv — alle Punkte ×1,5 diese Frage", "is-boost");
    } else if (msg.powerUpId === "time_plus") {
      showArenaToast("Zeit um 5 Sekunden verlängert", "is-time");
      bumpTimer();
    }
    hostArenaUsed = true;
    setArenaButtonsEnabled(false);
    btnBoost.classList.add("is-spent");
    btnTimePlus.classList.add("is-spent");
  });

  connection.on("PowerUpUsed", (msg) => {
    if (msg.endsAtUtc) {
      updateEndsAtFromServer(msg.endsAtUtc);
      bumpTimer();
      showArenaToast("Spieler: Extra-Zeit (+5s)", "is-time");
    }
  });

  connection.on("PowerUpError", (msg) => {
    if (questionOpen) {
      showArenaToast(msg.error || "Power-Up fehlgeschlagen");
    }
  });

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt || !startedAt) return;
      const ms = endsAt - Date.now();
      if (!questionOpen) {
        timerEl.textContent = "0s";
        timerEl.classList.remove("is-urgent", "is-critical");
        return;
      }
      if (ms <= 0) {
        timerEl.textContent = "0s · warte auf Server…";
        timerEl.classList.add("is-critical");
        return;
      }
      timerEl.textContent = `${Math.ceil(ms / 1000)}s`;
      setTimerUrgency(ms);
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }

  function stopTimer() {
    if (timerHandle) clearInterval(timerHandle);
    timerHandle = null;
  }

  btnCreate.addEventListener("click", () => {
    if (!quizSelect.value) {
      setLobbyStatus("Bitte zuerst ein Quiz wählen.", "is-error");
      return;
    }
    setLobbyStatus("Raum wird erstellt…", "is-loading");
    btnCreate.disabled = true;
    const asyncRace = readAsyncRaceConfig();
    hostAsyncRace = asyncRace.enabled;
    let exam = readExamConfig();
    const autoAdvance = asyncRace.enabled
      ? { enabled: false, delaySeconds: 5 }
      : readAutoAdvanceConfig();
    if (asyncRace.enabled) {
      exam = { enabled: false, tags: [] };
    }
    const config = exam.enabled ? null : readPowerUpConfig();
    connection
      .invoke("CreateRoom", quizSelect.value, config, autoAdvance, exam, asyncRace)
      .catch((err) => {
        btnCreate.disabled = false;
        setLobbyStatus(`Raum erstellen fehlgeschlagen: ${err}`, "is-error");
      });
  });

  quizSelect.addEventListener("change", () => {
    loadQuizTags(quizSelect.value);
  });
  $("async-race-enabled")?.addEventListener("change", () => {
    if ($("async-race-enabled")?.checked) {
      if ($("exam-enabled")) $("exam-enabled").checked = false;
      if ($("aa-enabled")) $("aa-enabled").checked = false;
    }
    syncAsyncRaceUi();
  });
  $("exam-enabled")?.addEventListener("change", () => {
    if ($("exam-enabled")?.checked && $("async-race-enabled")) {
      $("async-race-enabled").checked = false;
    }
    syncExamUi();
    syncAsyncRaceUi();
  });
  $("aa-enabled")?.addEventListener("change", () => {
    if ($("aa-enabled")?.checked && $("async-race-enabled")) {
      $("async-race-enabled").checked = false;
    }
    syncAsyncRaceUi();
  });
  btnStart.addEventListener("click", () => {
    btnStart.disabled = true;
    connection.invoke("StartGame").catch((err) => {
      btnStart.disabled = false;
      setLobbyStatus(`Start fehlgeschlagen: ${err}`, "is-error");
    });
  });
  btnNext.addEventListener("click", () => connection.invoke("NextQuestion"));

  btnBoost.addEventListener("click", () => {
    if (!questionOpen || hostArenaUsed) return;
    flashClass(btnBoost, "is-activating", 400);
    connection.invoke("HostArenaEvent", "boost_all");
  });
  btnTimePlus.addEventListener("click", () => {
    if (!questionOpen || hostArenaUsed) return;
    flashClass(btnTimePlus, "is-activating", 400);
    connection.invoke("HostArenaEvent", "time_plus");
  });

  connection.onreconnected(async () => {
    if (!roomCode) return;
    try {
      await connection.invoke("RejoinHost", roomCode);
      setLobbyStatus("Wieder verbunden.", "is-ok");
    } catch (err) {
      setLobbyStatus(`Reconnect fehlgeschlagen: ${err}`, "is-error");
    }
  });

  const btnShutdown = $("btn-shutdown");
  if (btnShutdown) {
    btnShutdown.addEventListener("click", async () => {
      if (!confirm("TrainArena-Server wirklich beenden?\nOffene Spiele enden sofort."))
        return;
      btnShutdown.disabled = true;
      try {
        const res = await fetch("/api/shutdown", { method: "POST" });
        if (!res.ok)
          throw new Error(`HTTP ${res.status}`);
        setLobbyStatus("Server wird beendet…", "is-ok");
        document.body.classList.add("is-shutting-down");
      } catch (err) {
        btnShutdown.disabled = false;
        setLobbyStatus(`Beenden fehlgeschlagen: ${err}`, "is-error");
      }
    });
  }

  Promise.all([connection.start(), loadQuizzes()])
    .then(async () => {
      if (!roomCode) {
        setLobbyStatus("Noch kein Raum.");
        return;
      }
      try {
        await connection.invoke("RejoinHost", roomCode);
        showJoinArtifacts(roomCode);
        btnCreate.disabled = true;
        quizSelect.disabled = true;
        disableLobbySetup();
        btnStart.classList.remove("hidden");
        setLobbyStatus("Wieder verbunden — warte auf Spieler…", "is-ok");
      } catch {
        sessionStorage.removeItem(STORAGE_ROOM);
        roomCode = "";
        setLobbyStatus("Noch kein Raum.");
      }
    })
    .catch((err) => {
      setLobbyStatus(`Verbindung fehlgeschlagen: ${err}`, "is-error");
      btnCreate.disabled = true;
    });
})();
