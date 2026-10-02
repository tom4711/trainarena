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
  const finishedEl = $("finished");
  const qrWrap = $("qr-wrap");
  const qrCanvas = $("qr-canvas");
  const joinUrlEl = $("join-url");

  const STORAGE_ROOM = "trainarena.host.room";

  let startedAt = null;
  let endsAt = null;
  let timerHandle = null;
  let questionOpen = false;
  let roomCode = sessionStorage.getItem(STORAGE_ROOM) || "";

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  async function loadQuizzes() {
    const res = await fetch("/api/quizzes");
    const quizzes = await res.json();
    quizSelect.innerHTML = "";
    quizzes.forEach((q) => {
      const opt = document.createElement("option");
      opt.value = q.id;
      opt.textContent = `${q.title} (${q.questionCount} Fragen)`;
      quizSelect.appendChild(opt);
    });
    if (quizzes.length === 0) {
      lobbyStatus.textContent = "Kein Quiz vorhanden — zuerst im Editor anlegen.";
      btnCreate.disabled = true;
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
    btnStart.classList.remove("hidden");
    lobbyStatus.textContent = "0 Spieler verbunden — warte auf Beitritte…";
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
    lobbyStatus.textContent =
      count === 0 ? "0 Spieler verbunden — warte auf Beitritte…" : `${count} Spieler verbunden`;
    btnStart.disabled = count < 1;
  });

  connection.on("JoinError", (msg) => {
    lobbyStatus.textContent = msg.error || "Fehler";
  });

  connection.on("QuestionStarted", (msg) => {
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    revealEl.classList.add("hidden");
    board.classList.add("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.add("hidden");
    questionOpen = true;
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
      li.textContent = `${i + 1}. ${opt}`;
      optionsEl.appendChild(li);
    });
    startedAt = new Date(msg.startedAtUtc);
    endsAt = new Date(msg.endsAtUtc);
    startTimer();
  });

  connection.on("QuestionEnded", (msg) => {
    questionOpen = false;
    stopTimer();
    timerEl.textContent = "0s";
    revealEl.textContent = `Richtige Antwort: Option ${msg.correctIndex + 1}`;
    revealEl.classList.remove("hidden");
  });

  connection.on("Leaderboard", (msg) => {
    boardList.innerHTML = "";
    (msg.entries || []).forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
      boardList.appendChild(li);
    });
    board.classList.remove("hidden");
    boardHint.classList.remove("hidden");
    btnNext.classList.remove("hidden");
    btnNext.textContent = msg.hasMoreQuestions ? "Nächste Frage" : "Abschluss zeigen";
  });

  connection.on("GameFinished", (msg) => {
    boardList.innerHTML = "";
    (msg.entries || []).forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
      boardList.appendChild(li);
    });
    board.classList.remove("hidden");
    boardHint.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.remove("hidden");
    progressEl.textContent = "Finale";
  });

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt || !startedAt) return;
      const ms = endsAt - Date.now();
      if (!questionOpen) {
        timerEl.textContent = "0s";
        return;
      }
      if (ms <= 0) {
        timerEl.textContent = "0s · warte auf Server…";
        return;
      }
      timerEl.textContent = `${Math.ceil(ms / 1000)}s`;
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }

  function stopTimer() {
    if (timerHandle) clearInterval(timerHandle);
    timerHandle = null;
  }

  btnCreate.addEventListener("click", () => connection.invoke("CreateRoom", quizSelect.value));
  btnStart.addEventListener("click", () => connection.invoke("StartGame"));
  btnNext.addEventListener("click", () => connection.invoke("NextQuestion"));

  connection.onreconnected(async () => {
    if (!roomCode) return;
    try {
      await connection.invoke("RejoinHost", roomCode);
      lobbyStatus.textContent = "Wieder verbunden.";
    } catch (err) {
      lobbyStatus.textContent = `Reconnect fehlgeschlagen: ${err}`;
    }
  });

  Promise.all([connection.start(), loadQuizzes()])
    .then(async () => {
      if (roomCode) {
        try {
          await connection.invoke("RejoinHost", roomCode);
          showJoinArtifacts(roomCode);
          btnCreate.disabled = true;
          quizSelect.disabled = true;
          btnStart.classList.remove("hidden");
        } catch {
          sessionStorage.removeItem(STORAGE_ROOM);
          roomCode = "";
        }
      }
    })
    .catch((err) => {
      lobbyStatus.textContent = `Verbindung fehlgeschlagen: ${err}`;
    });
})();
