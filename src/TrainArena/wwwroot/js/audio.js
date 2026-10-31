(() => {
  const PATHS = {
    bgm: "/audio/bgm-host.ogg",
    correct: "/audio/sfx-correct.ogg",
    wrong: "/audio/sfx-wrong.ogg",
    powerup: "/audio/sfx-powerup.ogg",
    hit: "/audio/sfx-hit.ogg",
    block: "/audio/sfx-block.ogg",
  };

  const DEFAULT_BGM_VOLUME = 0.22;
  const DEFAULT_SFX_VOLUME = 0.55;

  function readBool(key, fallback) {
    try {
      const v = localStorage.getItem(key);
      if (v === null) return fallback;
      return v === "1" || v === "true";
    } catch {
      return fallback;
    }
  }

  function readNumber(key, fallback) {
    try {
      const stored = localStorage.getItem(key);
      if (stored === null) return fallback;
      const v = Number(stored);
      return Number.isFinite(v) ? v : fallback;
    } catch {
      return fallback;
    }
  }

  function write(key, value) {
    try {
      localStorage.setItem(key, String(value));
    } catch {
      /* private mode */
    }
  }

  /**
   * Shared audio helper for Host BGM and Player SFX.
   * Unlock via user gesture (createRoom / join / mute toggle) before play().
   */
  function createAudioController({ role }) {
    const isHost = role === "host";
    const muteKey = isHost ? "trainarena.host.audio.muted" : "trainarena.player.audio.muted";
    const volKey = isHost ? "trainarena.host.audio.volume" : "trainarena.player.audio.volume";

    let muted = readBool(muteKey, false);
    let volume = Math.min(1, Math.max(0, readNumber(volKey, isHost ? DEFAULT_BGM_VOLUME : DEFAULT_SFX_VOLUME)));
    let unlocked = false;
    let bgm = null;
    const sfxCache = new Map();

    function ensureBgm() {
      if (!isHost) return null;
      if (!bgm) {
        bgm = new Audio(PATHS.bgm);
        bgm.loop = true;
        bgm.preload = "auto";
      }
      bgm.volume = muted ? 0 : volume;
      return bgm;
    }

    function unlock() {
      unlocked = true;
      if (isHost) {
        const a = ensureBgm();
        if (!a || muted) return;
        const p = a.play();
        if (p && typeof p.catch === "function") p.catch(() => {});
      }
    }

    function setMuted(next) {
      muted = !!next;
      write(muteKey, muted ? "1" : "0");
      if (isHost) {
        const a = ensureBgm();
        if (!a) return;
        a.volume = muted ? 0 : volume;
        if (!muted && unlocked) {
          const p = a.play();
          if (p && typeof p.catch === "function") p.catch(() => {});
        } else if (muted) {
          a.pause();
        }
      }
    }

    function setVolume(next) {
      volume = Math.min(1, Math.max(0, Number(next) || 0));
      write(volKey, volume);
      if (isHost) {
        const a = ensureBgm();
        if (a) a.volume = muted ? 0 : volume;
      }
    }

    function playSfx(name) {
      if (isHost || muted || !unlocked) return;
      const src = PATHS[name];
      if (!src) return;
      let base = sfxCache.get(name);
      if (!base) {
        base = new Audio(src);
        base.preload = "auto";
        sfxCache.set(name, base);
      }
      const shot = base.cloneNode();
      shot.volume = volume;
      const p = shot.play();
      if (p && typeof p.catch === "function") p.catch(() => {});
    }

    function playPowerUpFx(kind) {
      if (kind === "shield_up" || kind === "attack_launch") playSfx("powerup");
      else if (kind === "attack_hit" || kind === "shield_break") playSfx("hit");
      else if (kind === "attack_blocked") playSfx("block");
    }

    function playAnswerResult(points) {
      if (typeof points !== "number") return;
      playSfx(points > 0 ? "correct" : "wrong");
    }

    /** Start/resume BGM after a user gesture (Host). */
    function startBgm() {
      if (!isHost) return;
      unlock();
    }

    function stopBgm() {
      if (bgm) {
        bgm.pause();
        try {
          bgm.currentTime = 0;
        } catch {
          /* ignore */
        }
      }
    }

    return {
      get muted() {
        return muted;
      },
      get volume() {
        return volume;
      },
      get unlocked() {
        return unlocked;
      },
      unlock,
      setMuted,
      setVolume,
      startBgm,
      stopBgm,
      playSfx,
      playPowerUpFx,
      playAnswerResult,
      paths: PATHS,
    };
  }

  function bindHostControls(audio, { muteBtn, volumeInput }) {
    if (!muteBtn || !volumeInput) return;

    function syncUi() {
      muteBtn.setAttribute("aria-pressed", audio.muted ? "true" : "false");
      muteBtn.textContent = audio.muted ? "Ton aus" : "Ton an";
      volumeInput.value = String(Math.round(audio.volume * 100));
      volumeInput.disabled = audio.muted;
    }

    muteBtn.addEventListener("click", () => {
      audio.unlock();
      audio.setMuted(!audio.muted);
      syncUi();
    });

    volumeInput.addEventListener("input", () => {
      audio.unlock();
      const pct = Number(volumeInput.value);
      audio.setVolume((Number.isFinite(pct) ? pct : 22) / 100);
      if (audio.muted && pct > 0) {
        audio.setMuted(false);
      }
      syncUi();
    });

    syncUi();
  }

  function bindPlayerMute(audio, muteBtn) {
    if (!muteBtn) return;

    function syncUi() {
      muteBtn.setAttribute("aria-pressed", audio.muted ? "true" : "false");
      muteBtn.textContent = audio.muted ? "SFX aus" : "SFX an";
    }

    muteBtn.addEventListener("click", () => {
      audio.unlock();
      audio.setMuted(!audio.muted);
      syncUi();
    });

    syncUi();
  }

  window.TrainArenaAudio = {
    createAudioController,
    bindHostControls,
    bindPlayerMute,
    PATHS,
    DEFAULT_BGM_VOLUME,
    DEFAULT_SFX_VOLUME,
  };
})();
