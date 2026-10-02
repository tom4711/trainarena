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
  const qProgress = $("q-progress");
  const waitHost = $("wait-host");

  let startedAt = null;
  let endsAt = null;
  let timerHandle = null;
  let answered = false;
  let questionOpen = false;

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
    boardPanel.classList.add("hidden");
  });

  connection.on("QuestionStarted", (msg) => {
    answered = false;
    questionOpen = true;
    waitPanel.classList.add("hidden");
    boardPanel.classList.add("hidden");
    questionPanel.classList.remove("hidden");
    qProgress.textContent = `Frage ${(msg.index ?? 0) + 1} von ${msg.totalQuestions ?? "?"}`;
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
    startedAt = new Date(msg.startedAtUtc);
    endsAt = new Date(msg.endsAtUtc);
    startTimer();
  });

  connection.on("AnswerAccepted", (msg) => {
    if (!msg.ok) {
      answerStatus.textContent = msg.error || "Antwort abgelehnt";
      if (questionOpen && !answered) {
        [...answers.querySelectorAll("button")].forEach((b) => (b.disabled = false));
      }
      return;
    }
    answered = true;
    disableAnswers();
    answerStatus.textContent = `Gesendet (+${msg.points} Punkte)`;
  });

  connection.on("QuestionEnded", () => {
    questionOpen = false;
    stopTimer();
    qTimer.textContent = "0s";
    disableAnswers();
    if (!answered) answerStatus.textContent = "Zeit abgelaufen";
  });

  connection.on("Leaderboard", (msg) => {
    showBoard(msg.entries || [], false, msg.hasMoreQuestions);
  });

  connection.on("GameFinished", (msg) => {
    showBoard(msg.entries || [], true, false);
  });

  function showBoard(entries, done, hasMore) {
    questionPanel.classList.add("hidden");
    boardPanel.classList.remove("hidden");
    boardList.innerHTML = "";
    entries.forEach((e) => {
      const li = document.createElement("li");
      li.textContent = `${e.nickname}: ${e.score}`;
      boardList.appendChild(li);
    });
    $("done").classList.toggle("hidden", !done);
    waitHost.classList.toggle("hidden", done || !hasMore);
    if (!done && !hasMore) {
      waitHost.textContent = "Warte auf den Host (Abschluss)…";
      waitHost.classList.remove("hidden");
    } else if (hasMore) {
      waitHost.textContent = "Warte auf den Host (nächste Frage)…";
    }
  }

  function submit(index, btn) {
    if (answered || !questionOpen) return;
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
      // Display-only countdown from server timestamps; QuestionEnded is authoritative.
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
