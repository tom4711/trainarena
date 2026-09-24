(() => {
  const $ = (id) => document.getElementById(id);
  const params = new URLSearchParams(location.search);
  if (params.get("code")) $("code").value = params.get("code").toUpperCase();

  const joinPanel = $("join");
  const waitPanel = $("wait");
  const questionPanel = $("question");
  const boardPanel = $("board");
  const joinError = $("join-error");
  const answers = $("answers");
  const answerStatus = $("answer-status");
  const boardList = $("board-list");
  const qTimer = $("q-timer");

  let endsAt = null;
  let timerHandle = null;
  let answered = false;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/game")
    .withAutomaticReconnect()
    .build();

  connection.on("JoinError", (msg) => {
    joinError.textContent = msg.error || "Beitritt fehlgeschlagen";
  });

  connection.on("LobbyState", () => {
    joinPanel.classList.add("hidden");
    waitPanel.classList.remove("hidden");
    questionPanel.classList.add("hidden");
  });

  connection.on("QuestionStarted", (msg) => {
    answered = false;
    waitPanel.classList.add("hidden");
    boardPanel.classList.add("hidden");
    questionPanel.classList.remove("hidden");
    $("q-text").textContent = msg.text;
    answerStatus.textContent = "";
    answers.innerHTML = "";
    (msg.options || []).forEach((opt, i) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = opt;
      btn.addEventListener("click", () => submit(i, btn));
      answers.appendChild(btn);
    });
    endsAt = new Date(msg.endsAtUtc);
    startTimer();
  });

  connection.on("AnswerAccepted", (msg) => {
    if (!msg.ok) {
      answerStatus.textContent = msg.error || "Antwort abgelehnt";
      return;
    }
    answered = true;
    disableAnswers();
    answerStatus.textContent = `Gesendet (+${msg.points} Punkte)`;
  });

  connection.on("QuestionEnded", () => {
    stopTimer();
    disableAnswers();
    if (!answered) answerStatus.textContent = "Zeit abgelaufen";
  });

  connection.on("Leaderboard", (msg) => {
    showBoard(msg.entries || [], false);
  });

  connection.on("GameFinished", (msg) => {
    showBoard(msg.entries || [], true);
  });

  function showBoard(entries, done) {
    questionPanel.classList.add("hidden");
    boardPanel.classList.remove("hidden");
    boardList.innerHTML = "";
    entries.forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
      boardList.appendChild(li);
    });
    $("done").classList.toggle("hidden", !done);
  }

  function submit(index, btn) {
    if (answered) return;
    disableAnswers();
    btn.style.outline = "2px solid #fff";
    connection.invoke("SubmitAnswer", index);
  }

  function disableAnswers() {
    [...answers.querySelectorAll("button")].forEach((b) => (b.disabled = true));
  }

  function startTimer() {
    stopTimer();
    const tick = () => {
      if (!endsAt) return;
      const ms = endsAt - Date.now();
      qTimer.textContent = ms <= 0 ? "0s" : `${Math.ceil(ms / 1000)}s`;
    };
    tick();
    timerHandle = setInterval(tick, 200);
  }

  function stopTimer() {
    if (timerHandle) clearInterval(timerHandle);
    timerHandle = null;
  }

  $("btn-join").addEventListener("click", async () => {
    joinError.textContent = "";
    const code = $("code").value.trim().toUpperCase();
    const nickname = $("nickname").value.trim();
    $("me").textContent = nickname;
    try {
      await connection.invoke("JoinRoom", code, nickname);
    } catch (err) {
      joinError.textContent = String(err);
    }
  });

  connection.start().catch((err) => {
    joinError.textContent = `Verbindung fehlgeschlagen: ${err}`;
  });
})();
