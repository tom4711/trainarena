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
  ];

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

  if (roomCode) $("code").value = roomCode;
  if (nickname) $("nickname").value = nickname;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  function countFor(id) {
    return inventory[id] ?? 0;
  }

  function refreshPowerUpButtons() {
    const buttons = powerupsEl.querySelectorAll("button[data-powerup]");
    const lock = answered || !questionOpen;
    buttons.forEach((btn) => {
      const id = btn.dataset.powerup;
      const n = countFor(id);
      const countSpan = btn.querySelector(".count");
      if (countSpan) countSpan.textContent = ` (${n})`;
      btn.disabled = lock || n <= 0;
    });
    const anyStock = PLAYER_POWERUPS.some((p) => countFor(p.id) > 0);
    powerupsEl.classList.toggle("hidden", !questionOpen || !anyStock);
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

  function usePowerUp(id) {
    if (answered || !questionOpen || countFor(id) <= 0) return;
    connection.invoke("UsePowerUp", id);
  }

  function applyMaskedOptions(indexes) {
    if (!indexes || !indexes.length) return;
    const buttons = [...answers.querySelectorAll("button")];
    indexes.forEach((i) => {
      const btn = buttons[i];
      if (btn) btn.classList.add("hidden");
    });
  }

  connection.on("JoinError", (msg) => {
    joinError.textContent = msg.error || "Beitritt fehlgeschlagen";
  });

  connection.on("InventoryUpdate", (msg) => {
    inventory = msg.counts || {};
    ensurePowerUpButtons();
    refreshPowerUpButtons();
  });

  connection.on("PowerUpUsed", (msg) => {
    if (!msg.ok) return;
    if (msg.endsAtUtc) {
      endsAt = new Date(msg.endsAtUtc);
      if (questionOpen) startTimer();
    }
    if (msg.maskedWrongIndexes && msg.maskedWrongIndexes.length) {
      applyMaskedOptions(msg.maskedWrongIndexes);
    }
  });

  connection.on("PowerUpError", (msg) => {
    if (questionOpen) {
      answerStatus.textContent = msg.error || "Power-Up fehlgeschlagen";
    }
  });

  connection.on("ArenaEvent", (msg) => {
    if (msg.endsAtUtc) {
      endsAt = new Date(msg.endsAtUtc);
      if (questionOpen) startTimer();
    }
    if (msg.powerUpId === "boost_all" && questionOpen) {
      answerStatus.textContent = "Team-Boost aktiv — Punkte ×1,5";
    }
  });

  connection.on("LobbyState", () => {
    joinPanel.classList.add("hidden");
    waitPanel.classList.remove("hidden");
    questionPanel.classList.add("hidden");
    powerupsEl.classList.add("hidden");
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
      answerStatus.textContent = msg.error || "Antwort abgelehnt";
      if (questionOpen) {
        [...answers.querySelectorAll("button")].forEach((b) => (b.disabled = false));
        refreshPowerUpButtons();
      }
      return;
    }
    disableAnswers();
    refreshPowerUpButtons();
    answerStatus.textContent = `Gesendet (+${msg.points} Punkte)`;
  });

  connection.on("QuestionEnded", () => {
    questionOpen = false;
    stopTimer();
    qTimer.textContent = "0s";
    disableAnswers();
    powerupsEl.classList.add("hidden");
    if (!answered) answerStatus.textContent = "Zeit abgelaufen";
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
    entries.forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
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
    btn.style.outline = "2px solid #fff";
    connection.invoke("SubmitAnswer", index);
  }

  function disableAnswers() {
    [...answers.querySelectorAll("button")].forEach((b) => (b.disabled = true));
  }

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt || !startedAt) return;
      const ms = endsAt - Date.now();
      if (!questionOpen) {
        qTimer.textContent = "0s";
        return;
      }
      if (ms <= 0) {
        qTimer.textContent = "0s · warte auf Server…";
        return;
      }
      qTimer.textContent = `${Math.ceil(ms / 1000)}s`;
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

  $("btn-join").addEventListener("click", async () => {
    joinError.textContent = "";
    const code = $("code").value.trim().toUpperCase();
    const nick = $("nickname").value.trim();
    try {
      await joinOrRejoin(code, nick, false);
    } catch (err) {
      try {
        await joinOrRejoin(code, nick, true);
      } catch (err2) {
        joinError.textContent = String(err2);
      }
    }
  });

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
    });
})();
