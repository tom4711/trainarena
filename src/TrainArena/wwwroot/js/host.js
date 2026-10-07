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
  const qrWrap = $("qr-wrap");
  const qrCanvas = $("qr-canvas");
  const joinUrlEl = $("join-url");
  const arenaActions = $("arena-actions");
  const btnBoost = $("btn-boost");
  const btnTimePlus = $("btn-time-plus");
  const arenaStatus = $("arena-status");

  const STORAGE_ROOM = "trainarena.host.room";

  let startedAt = null;
  let endsAt = null;
  let timerHandle = null;
  let questionOpen = false;
  let hostArenaUsed = false;
  let autoAdvanceHandle = null;
  let roomCode = sessionStorage.getItem(STORAGE_ROOM) || "";

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

  function renderBoardEntries(entries) {
    boardList.innerHTML = "";
    (entries || []).forEach((e, i) => {
      const li = document.createElement("li");
      li.innerHTML =
        `<span class="rank">${i + 1}.</span>` +
        `<span class="nick"></span>` +
        `<span class="score"></span>`;
      li.querySelector(".nick").textContent = e.nickname;
      li.querySelector(".score").textContent = String(e.score);
      boardList.appendChild(li);
    });
  }

  function setTimerUrgency(msLeft) {
    timerEl.classList.remove("is-urgent", "is-critical");
    if (!questionOpen || msLeft <= 0) return;
    const sec = Math.ceil(msLeft / 1000);
    if (sec <= 5) timerEl.classList.add("is-critical");
    else if (sec <= 10) timerEl.classList.add("is-urgent");
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
    if (window.QRCode) {
      QRCode.toCanvas(qrCanvas, url, { width: 180, margin: 1 }, (err) => {
        if (err) console.error(err);
      });
    }
  }

  connection.on("RoomCreated", (msg) => {
    showJoinArtifacts(msg.code);
    btnCreate.disabled = true;
    quizSelect.disabled = true;
    disableLobbySetup();
    btnStart.classList.remove("hidden");
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
    clearAutoAdvanceCountdown();
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    revealEl.classList.add("hidden");
    board.classList.add("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.add("hidden");
    questionOpen = true;
    hostArenaUsed = false;
    setArenaVisible(true);
    setArenaButtonsEnabled(true);
    progressEl.textContent = `Frage ${(msg.index ?? 0) + 1} von ${msg.totalQuestions ?? "?"}`;
    questionText.textContent = msg.text;
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
  });

  connection.on("Leaderboard", (msg) => {
    clearAutoAdvanceCountdown();
    setArenaVisible(false);
    renderBoardEntries(msg.entries);
    board.classList.remove("hidden");
    boardHint.classList.remove("hidden");
    btnNext.classList.remove("hidden");
    btnNext.textContent = msg.hasMoreQuestions ? "Nächste Frage" : "Abschluss zeigen";
  });

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
    setArenaVisible(false);
    renderBoardEntries(msg.entries);
    board.classList.remove("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.remove("hidden");
    progressEl.textContent = "Finale";
  });

  connection.on("ArenaEvent", (msg) => {
    updateEndsAtFromServer(msg.endsAtUtc);
    if (msg.powerUpId === "boost_all") {
      arenaStatus.textContent = "Team-Boost aktiv — alle Punkte ×1,5 diese Frage";
      arenaStatus.classList.remove("hidden");
    } else if (msg.powerUpId === "time_plus") {
      arenaStatus.textContent = "Zeit um 5 Sekunden verlängert";
      arenaStatus.classList.remove("hidden");
    }
    hostArenaUsed = true;
    setArenaButtonsEnabled(false);
  });

  connection.on("PowerUpUsed", (msg) => {
    if (msg.endsAtUtc) updateEndsAtFromServer(msg.endsAtUtc);
  });

  connection.on("PowerUpError", (msg) => {
    if (questionOpen) {
      arenaStatus.textContent = msg.error || "Power-Up fehlgeschlagen";
      arenaStatus.classList.remove("hidden");
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
    const config = readPowerUpConfig();
    const autoAdvance = readAutoAdvanceConfig();
    connection.invoke("CreateRoom", quizSelect.value, config, autoAdvance).catch((err) => {
      btnCreate.disabled = false;
      setLobbyStatus(`Raum erstellen fehlgeschlagen: ${err}`, "is-error");
    });
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
    connection.invoke("HostArenaEvent", "boost_all");
  });
  btnTimePlus.addEventListener("click", () => {
    if (!questionOpen || hostArenaUsed) return;
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
