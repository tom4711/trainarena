(() => {
  const $ = (id) => document.getElementById(id);
  const params = new URLSearchParams(location.search);
  if (params.get("code")) $("code").value = params.get("code").toUpperCase();

  const STORAGE_CODE = "trainarena.player.code";
  const STORAGE_NICK = "trainarena.player.nickname";

  const joinPanel = $("join");
  const waitPanel = $("wait");
  const questionPanel = $("question");
  const boardPanel = $("board");
  const joinError = $("join-error");
  const answers = $("answers");
  const answerStatus = $("answer-status");
  const boardList = $("board-list");
  const qTimer = $("q-timer");
  const qProgress = $("q-progress");
  const waitHost = $("wait-host");
  const boardAutoAdvance = $("board-auto-advance");
  const powerupsEl = $("powerups");

  const PLAYER_POWERUPS = [
    { id: "fifty_fifty", label: "50/50" },
    { id: "double", label: "Double" },
    { id: "extra_time", label: "Extra-Zeit" },
    { id: "shield", label: "Shield" },
    { id: "disrupt", label: "Störimpuls" },
  ];

  const targetPicker = $("target-picker");
  const targetList = $("target-list");
  const fxOverlay = $("fx-overlay");
  const fxTitle = $("fx-title");
  const fxSub = $("fx-sub");

  let startedAt = null;
  let endsAt = null;
  let timerHandle = null;
  let answered = false;
  let questionOpen = false;
  let inventory = {};
  let autoAdvanceHandle = null;
  let boardWaitingForHost = false;
  let roomCode = sessionStorage.getItem(STORAGE_CODE) || $("code").value.trim().toUpperCase();
  let nickname = sessionStorage.getItem(STORAGE_NICK) || "";
  let lobbyPlayers = [];
  let fxTimer = null;
  const fxQueue = [];
  let fxShowing = false;

  if (roomCode) $("code").value = roomCode;
  if (nickname) $("nickname").value = nickname;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const activePowerUps = new Set();
  let prevInventory = {};

  function countFor(id) {
    return inventory[id] ?? 0;
  }

  function powerUpButton(id) {
    return powerupsEl.querySelector(`button[data-powerup="${id}"]`);
  }

  function flashClass(el, className, ms = 420) {
    if (!el) return;
    el.classList.remove(className);
    // force reflow so re-trigger works
    void el.offsetWidth;
    el.classList.add(className);
    window.setTimeout(() => el.classList.remove(className), ms);
  }

  function showPuToast(text) {
    answerStatus.className = "is-pu-toast";
    answerStatus.textContent = text;
  }

  function bumpTimer() {
    flashClass(qTimer, "is-extended", 560);
  }

  function refreshPowerUpButtons() {
    const buttons = powerupsEl.querySelectorAll("button[data-powerup]");
    const lock = answered || !questionOpen;
    buttons.forEach((btn) => {
      const id = btn.dataset.powerup;
      const n = countFor(id);
      const countSpan = btn.querySelector(".count");
      if (countSpan) countSpan.textContent = ` (${n})`;
      const keepActive = activePowerUps.has(id);
      btn.classList.toggle("is-active", keepActive);
      btn.disabled = (lock || n <= 0) && !keepActive;
      if (keepActive) btn.disabled = true;
    });
    const anyStock = PLAYER_POWERUPS.some((p) => countFor(p.id) > 0);
    const anyActive = activePowerUps.size > 0;
    powerupsEl.classList.toggle("hidden", !questionOpen || (!anyStock && !anyActive));
  }

  function ensurePowerUpButtons() {
    if (powerupsEl.dataset.built === "1") return;
    powerupsEl.dataset.built = "1";
    powerupsEl.innerHTML = "";
    PLAYER_POWERUPS.forEach((p) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.dataset.powerup = p.id;
      btn.innerHTML = `${p.label}<span class="count"> (0)</span>`;
      btn.addEventListener("click", () => usePowerUp(p.id));
      powerupsEl.appendChild(btn);
    });
  }

  function showFx(kind, title, sub) {
    fxQueue.push({ kind, title, sub });
    if (!fxShowing) drainFxQueue();
  }

  function drainFxQueue() {
    if (!fxOverlay || fxQueue.length === 0) {
      fxShowing = false;
      return;
    }
    fxShowing = true;
    const { kind, title, sub } = fxQueue.shift();
    if (fxTimer) clearTimeout(fxTimer);
    fxOverlay.className = `fx-overlay is-${kind}`;
    fxTitle.textContent = title;
    fxSub.textContent = sub || "";
    fxOverlay.classList.remove("hidden");
    const ms = reducedMotion ? 1600 : 1100;
    fxTimer = window.setTimeout(() => {
      fxOverlay.classList.add("hidden");
      fxOverlay.className = "fx-overlay hidden";
      drainFxQueue();
    }, ms);
  }

  function hideTargetPicker() {
    targetPicker?.classList.add("hidden");
    if (targetList) targetList.innerHTML = "";
  }

  function openTargetPicker() {
    const opponents = lobbyPlayers.filter(
      (p) => p && p.toLowerCase() !== nickname.toLowerCase(),
    );
    if (!targetList || !targetPicker) {
      invokeDisrupt(null);
      return;
    }
    if (opponents.length === 0) {
      answerStatus.className = "error";
      answerStatus.textContent = "Kein gültiges Ziel";
      return;
    }
    targetList.innerHTML = "";
    opponents.forEach((name) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = name;
      btn.addEventListener("click", () => {
        hideTargetPicker();
        invokeDisrupt(name);
      });
      targetList.appendChild(btn);
    });
    targetPicker.classList.remove("hidden");
  }

  function invokeDisrupt(targetNickname) {
    const btn = powerUpButton("disrupt");
    flashClass(btn, "is-activating", 400);
    connection.invoke("UsePowerUp", "disrupt", targetNickname || null);
  }

  function usePowerUp(id) {
    if (answered || !questionOpen || countFor(id) <= 0) return;
    if (id === "disrupt") {
      openTargetPicker();
      return;
    }
    const btn = powerUpButton(id);
    flashClass(btn, "is-activating", 400);
    connection.invoke("UsePowerUp", id, null);
  }

  function applyMaskedOptions(indexes) {
    if (!indexes || !indexes.length) return;
    const buttons = [...answers.querySelectorAll("button")];
    indexes.forEach((i) => {
      const btn = buttons[i];
      if (!btn || btn.classList.contains("is-masked")) return;
      if (reducedMotion) {
        btn.classList.add("is-masked", "hidden");
        return;
      }
      btn.classList.add("is-masking");
      window.setTimeout(() => {
        btn.classList.remove("is-masking");
        btn.classList.add("is-masked", "hidden");
      }, 420);
    });
  }

  function animateInventoryConsume(nextCounts) {
    PLAYER_POWERUPS.forEach((p) => {
      const before = prevInventory[p.id] ?? 0;
      const after = nextCounts[p.id] ?? 0;
      if (after < before) {
        const btn = powerUpButton(p.id);
        const countSpan = btn?.querySelector(".count");
        flashClass(countSpan, "is-consumed", 450);
      }
    });
    prevInventory = { ...nextCounts };
  }

  function markPowerUpActive(id) {
    if (id === "double" || id === "shield") {
      activePowerUps.add(id);
    }
    refreshPowerUpButtons();
  }

  function clearActivePowerUps() {
    activePowerUps.clear();
    powerupsEl.querySelectorAll("button.is-active").forEach((b) => b.classList.remove("is-active"));
  }

  connection.on("JoinError", (msg) => {
    joinError.textContent = msg.error || "Beitritt fehlgeschlagen";
  });

  connection.on("InventoryUpdate", (msg) => {
    const next = msg.counts || {};
    ensurePowerUpButtons();
    if (Object.keys(prevInventory).length) {
      animateInventoryConsume(next);
    } else {
      prevInventory = { ...next };
    }
    inventory = next;
    refreshPowerUpButtons();
  });

  connection.on("PowerUpUsed", (msg) => {
    if (!msg.ok) return;
    const id = msg.powerUpId;
    const btn = powerUpButton(id);
    if (btn) flashClass(btn, "is-activating", 400);

    if (msg.endsAtUtc) {
      endsAt = new Date(msg.endsAtUtc);
      if (questionOpen) {
        startTimer();
        bumpTimer();
      }
    }
    if (msg.maskedWrongIndexes && msg.maskedWrongIndexes.length) {
      applyMaskedOptions(msg.maskedWrongIndexes);
      showPuToast("50/50 — zwei Optionen entfernt");
    } else if (id === "extra_time") {
      showPuToast("Extra-Zeit — +5 Sekunden");
    } else if (id === "double") {
      showPuToast("Double aktiv — nächste richtige Antwort ×2");
      markPowerUpActive("double");
    } else if (id === "shield") {
      showPuToast("Shield aktiv");
      markPowerUpActive("shield");
    } else if (id === "disrupt") {
      if (msg.blockedByShield) {
        showPuToast(`Schild blockt — ${msg.targetNickname || "Ziel"} geschützt`);
      } else {
        showPuToast(`Störimpuls trifft ${msg.targetNickname || "Ziel"}`);
      }
    }
  });

  connection.on("PowerUpFx", (msg) => {
    const kind = msg?.kind;
    const actor = msg?.actorNickname || "?";
    const target = msg?.targetNickname || "?";
    const me = nickname;
    if (kind === "shield_up") {
      showFx(kind, "Schild aktiv", actor === me ? "Du bist geschützt" : `${actor} ist geschützt`);
      if (actor === me) markPowerUpActive("shield");
    } else if (kind === "shield_break") {
      showFx(
        kind,
        "Schild zerstört",
        actor === me ? "Dein Schild ist gebrochen" : `Schild von ${actor} zerstört`,
      );
      if (actor === me) {
        activePowerUps.delete("shield");
        refreshPowerUpButtons();
      }
    } else if (kind === "attack_launch") {
      showFx(
        kind,
        "Störimpuls!",
        actor === me ? `Du greifst ${target} an` : `${actor} greift ${target} an`,
      );
    } else if (kind === "attack_hit") {
      showFx(
        kind,
        target === me ? "Du wurdest gestört!" : "Treffer!",
        target === me
          ? "Deine nächste Antwort zählt nicht"
          : `${target} ist gestört`,
      );
      if (target === me) {
        showPuToast("Gestört — nächste Antwort zählt nicht");
      }
    } else if (kind === "attack_blocked") {
      showFx(
        kind,
        "Geblockt!",
        target === me
          ? "Dein Schild hat gehalten"
          : `${target} blockt den Angriff`,
      );
    }
  });

  connection.on("PowerUpError", (msg) => {
    if (questionOpen) {
      answerStatus.className = "error";
      answerStatus.textContent = msg.error || "Power-Up fehlgeschlagen";
    }
  });

  connection.on("ArenaEvent", (msg) => {
    if (msg.endsAtUtc) {
      endsAt = new Date(msg.endsAtUtc);
      if (questionOpen) {
        startTimer();
        bumpTimer();
      }
    }
    if (!questionOpen) return;
    if (msg.powerUpId === "boost_all") {
      showPuToast("Team-Boost aktiv — Punkte ×1,5");
    } else if (msg.powerUpId === "time_plus") {
      showPuToast("Host: Zeit +5 Sekunden");
    }
  });

  connection.on("LobbyState", (msg) => {
    lobbyPlayers = (msg?.players || [])
      .filter((p) => p && p.isConnected !== false)
      .map((p) => p.nickname)
      .filter(Boolean);
    joinPanel.classList.add("hidden");
    waitPanel.classList.remove("hidden");
    questionPanel.classList.add("hidden");
    powerupsEl.classList.add("hidden");
    hideTargetPicker();
    if (boardPanel.classList.contains("hidden") === false && !questionOpen) {
      // keep board if mid-round sync already showed it
    } else {
      boardPanel.classList.add("hidden");
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
    waitHost.classList.add("hidden");
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
    clearAutoAdvanceCountdown();
    answered = false;
    questionOpen = true;
    clearActivePowerUps();
    waitPanel.classList.add("hidden");
    boardPanel.classList.add("hidden");
    questionPanel.classList.remove("hidden");
    qProgress.textContent = `Frage ${(msg.index ?? 0) + 1} von ${msg.totalQuestions ?? "?"}`;
    $("q-text").textContent = msg.text;
    const qImage = $("q-image");
    if (msg.imageUrl) {
      qImage.src = msg.imageUrl;
      qImage.classList.remove("hidden");
    } else {
      qImage.removeAttribute("src");
      qImage.classList.add("hidden");
    }
    answerStatus.className = "muted";
    answerStatus.textContent = "";
    answers.innerHTML = "";
    (msg.options || []).forEach((opt, i) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = opt;
      btn.addEventListener("click", () => submit(i, btn));
      answers.appendChild(btn);
    });
    startedAt = new Date(msg.startedAtUtc);
    endsAt = new Date(msg.endsAtUtc);
    ensurePowerUpButtons();
    refreshPowerUpButtons();
    startTimer();
  });

  connection.on("AnswerAccepted", (msg) => {
    if (!msg.ok) {
      answered = false;
      answerStatus.className = "error";
      answerStatus.textContent = msg.error || "Antwort abgelehnt";
      if (questionOpen) {
        [...answers.querySelectorAll("button")].forEach((b) => {
          b.disabled = false;
          b.classList.remove("is-selected");
        });
        refreshPowerUpButtons();
      }
      return;
    }
    disableAnswers();
    refreshPowerUpButtons();
    answerStatus.className = "status-ok";
    answerStatus.textContent =
      msg.points > 0 ? `Gesendet (+${msg.points} Punkte)` : "Gesendet";
  });

  connection.on("QuestionEnded", () => {
    questionOpen = false;
    stopTimer();
    qTimer.textContent = "0s";
    qTimer.classList.remove("is-urgent", "is-critical", "is-extended");
    disableAnswers();
    clearActivePowerUps();
    powerupsEl.classList.add("hidden");
    if (!answered) {
      answerStatus.className = "status-warn";
      answerStatus.textContent = "Zeit abgelaufen";
    }
  });

  connection.on("Leaderboard", (msg) => {
    clearAutoAdvanceCountdown();
    showBoard(msg.entries || [], false, msg.hasMoreQuestions);
  });

  connection.on("GameFinished", (msg) => {
    clearAutoAdvanceCountdown();
    showBoard(msg.entries || [], true, false);
  });

  connection.on("AutoAdvanceScheduled", (msg) => {
    if (msg?.advancesAtUtc) startAutoAdvanceCountdown(msg.advancesAtUtc);
  });

  connection.on("AutoAdvanceCancelled", () => {
    clearAutoAdvanceCountdown();
    if (boardWaitingForHost) {
      waitHost.classList.remove("hidden");
    }
  });

  function showBoard(entries, done, hasMore) {
    questionPanel.classList.add("hidden");
    boardPanel.classList.remove("hidden");
    powerupsEl.classList.add("hidden");
    boardList.innerHTML = "";
    entries.forEach((e, i) => {
      const li = document.createElement("li");
      li.innerHTML =
        `<span class="rank">${i + 1}.</span>` +
        `<span class="nick"></span>` +
        `<span class="score"></span>`;
      li.querySelector(".nick").textContent = e.nickname;
      li.querySelector(".score").textContent = String(e.score);
      boardList.appendChild(li);
    });
    $("done").classList.toggle("hidden", !done);
    boardWaitingForHost = !done;
    if (done) {
      waitHost.classList.add("hidden");
    } else if (hasMore) {
      waitHost.textContent = "Warte auf den Host (nächste Frage)…";
      waitHost.classList.remove("hidden");
    } else {
      waitHost.textContent = "Warte auf den Host (Abschluss)…";
      waitHost.classList.remove("hidden");
    }
  }

  function submit(index, btn) {
    if (answered || !questionOpen) return;
    answered = true;
    disableAnswers();
    refreshPowerUpButtons();
    btn.classList.add("is-selected");
    answerStatus.className = "muted";
    answerStatus.textContent = "Sende…";
    connection.invoke("SubmitAnswer", index).catch((err) => {
      answered = false;
      [...answers.querySelectorAll("button")].forEach((b) => {
        b.disabled = false;
        b.classList.remove("is-selected");
      });
      refreshPowerUpButtons();
      answerStatus.className = "error";
      answerStatus.textContent = `Senden fehlgeschlagen: ${err}`;
    });
  }

  function disableAnswers() {
    [...answers.querySelectorAll("button")].forEach((b) => (b.disabled = true));
  }

  function setTimerUrgency(msLeft) {
    qTimer.classList.remove("is-urgent", "is-critical");
    if (!questionOpen || msLeft <= 0) return;
    const sec = Math.ceil(msLeft / 1000);
    if (sec <= 5) qTimer.classList.add("is-critical");
    else if (sec <= 10) qTimer.classList.add("is-urgent");
  }

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt || !startedAt) return;
      const ms = endsAt - Date.now();
      if (!questionOpen) {
        qTimer.textContent = "0s";
        qTimer.classList.remove("is-urgent", "is-critical");
        return;
      }
      if (ms <= 0) {
        qTimer.textContent = "0s · warte auf Server…";
        qTimer.classList.add("is-critical");
        return;
      }
      qTimer.textContent = `${Math.ceil(ms / 1000)}s`;
      setTimerUrgency(ms);
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }

  function stopTimer() {
    if (timerHandle) clearInterval(timerHandle);
    timerHandle = null;
  }

  async function joinOrRejoin(code, nick, preferRejoin) {
    roomCode = code;
    nickname = nick;
    sessionStorage.setItem(STORAGE_CODE, code);
    sessionStorage.setItem(STORAGE_NICK, nick);
    $("me").textContent = nick;
    if (preferRejoin) {
      await connection.invoke("RejoinRoom", code, nick);
    } else {
      try {
        await connection.invoke("JoinRoom", code, nick);
      } catch {
        await connection.invoke("RejoinRoom", code, nick);
      }
    }
  }

  async function tryJoin() {
    joinError.textContent = "";
    const code = $("code").value.trim().toUpperCase();
    const nick = $("nickname").value.trim();
    if (!code || code.length < 4) {
      joinError.textContent = "Bitte einen gültigen Raum-Code eingeben.";
      return;
    }
    if (!nick) {
      joinError.textContent = "Bitte einen Nickname eingeben.";
      return;
    }
    const btn = $("btn-join");
    btn.classList.add("is-busy");
    btn.disabled = true;
    btn.textContent = "Beitreten…";
    try {
      await joinOrRejoin(code, nick, false);
    } catch (err) {
      try {
        await joinOrRejoin(code, nick, true);
      } catch (err2) {
        joinError.textContent = String(err2?.message || err2);
      }
    } finally {
      btn.classList.remove("is-busy");
      btn.disabled = false;
      btn.textContent = "Beitreten";
    }
  }

  $("btn-join").addEventListener("click", () => {
    tryJoin();
  });
  $("nickname").addEventListener("keydown", (ev) => {
    if (ev.key === "Enter") {
      ev.preventDefault();
      tryJoin();
    }
  });
  $("code").addEventListener("keydown", (ev) => {
    if (ev.key === "Enter") {
      ev.preventDefault();
      $("nickname").focus();
    }
  });
  $("btn-target-cancel")?.addEventListener("click", () => hideTargetPicker());

  connection.onreconnected(async () => {
    if (!roomCode || !nickname) return;
    try {
      await connection.invoke("RejoinRoom", roomCode, nickname);
    } catch (err) {
      joinError.textContent = `Reconnect fehlgeschlagen: ${err}`;
    }
  });

  connection
    .start()
    .then(async () => {
      if (roomCode && nickname) {
        try {
          await joinOrRejoin(roomCode, nickname, true);
        } catch {
          // stay on join form
        }
      }
    })
    .catch((err) => {
      joinError.textContent = `Verbindung fehlgeschlagen: ${err}`;
      $("btn-join").disabled = true;
    });
})();
