(() => {
  const $ = (id) => document.getElementById(id);
  const setup = $("setup");
  const live = $("live");
  const btnCreate = $("btn-create");
  const btnStart = $("btn-start");
  const btnNext = $("btn-next");
  const roomCodeEl = $("room-code");
  const lobbyStatus = $("lobby-status");
  const playerList = $("player-list");
  const questionText = $("question-text");
  const optionsEl = $("options");
  const timerEl = $("timer");
  const revealEl = $("reveal");
  const board = $("board");
  const boardList = $("board-list");
  const finishedEl = $("finished");

  let endsAt = null;
  let timerHandle = null;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  connection.on("RoomCreated", (msg) => {
    roomCodeEl.textContent = msg.code;
    roomCodeEl.classList.remove("hidden");
    btnCreate.disabled = true;
    btnStart.classList.remove("hidden");
    lobbyStatus.textContent = "Warte auf Spieler…";
  });

  connection.on("LobbyState", (msg) => {
    playerList.innerHTML = "";
    (msg.players || []).forEach((p) => {
      const li = document.createElement("li");
      li.textContent = p.nickname;
      playerList.appendChild(li);
    });
    const count = msg.connectedCount ?? 0;
    lobbyStatus.textContent = count === 0 ? "Warte auf Spieler…" : `${count} Spieler verbunden`;
    btnStart.disabled = count < 1;
  });

  connection.on("QuestionStarted", (msg) => {
    setup.classList.add("hidden");
    live.classList.remove("hidden");
    revealEl.classList.add("hidden");
    board.classList.add("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.add("hidden");
    questionText.textContent = msg.text;
    optionsEl.innerHTML = "";
    (msg.options || []).forEach((opt, i) => {
      const li = document.createElement("li");
      li.textContent = `${i + 1}. ${opt}`;
      optionsEl.appendChild(li);
    });
    endsAt = new Date(msg.endsAtUtc);
    startTimer();
  });

  connection.on("QuestionEnded", (msg) => {
    stopTimer();
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
    btnNext.classList.remove("hidden");
  });

  connection.on("GameFinished", (msg) => {
    boardList.innerHTML = "";
    (msg.entries || []).forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
      boardList.appendChild(li);
    });
    board.classList.remove("hidden");
    btnNext.classList.add("hidden");
    finishedEl.classList.remove("hidden");
  });

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt) return;
      const ms = endsAt - Date.now();
      timerEl.textContent = ms <= 0 ? "0s" : `${Math.ceil(ms / 1000)}s`;
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }

  function stopTimer() {
    if (timerHandle) clearInterval(timerHandle);
    timerHandle = null;
  }

  btnCreate.addEventListener("click", () => connection.invoke("CreateRoom"));
  btnStart.addEventListener("click", () => connection.invoke("StartDemo"));
  btnNext.addEventListener("click", () => connection.invoke("NextQuestion"));

  connection.start().catch((err) => {
    lobbyStatus.textContent = `Verbindung fehlgeschlagen: ${err}`;
  });
})();
