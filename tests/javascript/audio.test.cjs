const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const path = require("node:path");
const { test } = require("node:test");
const vm = require("node:vm");

const webRoot = path.resolve(__dirname, "../../src/TrainArena/wwwroot");

function createEnvironment(savedSettings = {}) {
  const settings = new Map(Object.entries(savedSettings));
  const plays = [];
  class Audio {
    constructor(src) { this.src = src; }
    cloneNode() { return new Audio(this.src); }
    play() {
      plays.push({ src: this.src, volume: this.volume });
      return Promise.resolve();
    }
    pause() {}
  }
  const context = vm.createContext({
    window: {},
    localStorage: {
      getItem: (key) => settings.get(key) ?? null,
      setItem: (key, value) => settings.set(key, value),
    },
    Audio,
  });
  function load(file) {
    vm.runInContext(readFileSync(path.join(webRoot, file), "utf8"), context, { filename: file });
  }
  load("js/audio.js");
  return { context, load, plays, audio: context.window.TrainArenaAudio };
}

for (const [role, defaultVolume] of [["host", 0.22], ["player", 0.55]]) {
  for (const [description, savedVolume, expectedVolume] of [
    ["missing volume uses the audible default", undefined, defaultVolume],
    ["saved zero stays silent", "0", 0],
    ["saved volume is restored", "0.37", 0.37],
  ]) {
    test(`${role}: ${description}`, () => {
      const settings = savedVolume === undefined ? {} : {
        [`trainarena.${role}.audio.volume`]: savedVolume,
      };
      const { audio, plays } = createEnvironment(settings);
      const controller = audio.createAudioController({ role });
      controller.unlock();
      if (role === "player") controller.playAnswerResult(100);

      assert.deepEqual(plays, [{
        src: role === "host" ? "/audio/bgm-host.ogg" : "/audio/sfx-correct.ogg",
        volume: expectedVolume,
      }]);
    });
  }
}

async function loadPlayer() {
  // Keep the volume nonzero so join/unlock regressions are independent of defaults.
  const env = createEnvironment({ "trainarena.player.audio.volume": "0.55" });
  const events = new Map();
  const elements = new Map();
  const messages = new Map();
  const calls = [];
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      value: id === "code" ? "ABCD" : id === "nickname" ? "Tester" : "",
      classList: { add() {}, remove() {}, toggle() {} },
      querySelector: () => null,
      querySelectorAll: () => [],
      setAttribute() {},
      addEventListener: (event, handler) => events.set(`${id}:${event}`, handler),
    });
    return elements.get(id);
  }
  const session = new Map();
  const connection = {
    on: (event, handler) => messages.set(event, handler),
    onreconnected() {},
    start: () => Promise.resolve(),
    invoke: async (...args) => { calls.push(args); },
  };
  Object.assign(env.context, {
    document: { getElementById: (id) => id === "app-version" ? null : element(id) },
    location: { search: "" },
    URLSearchParams,
    sessionStorage: {
      getItem: (key) => session.get(key) ?? null,
      setItem: (key, value) => session.set(key, value),
    },
    signalR: {
      HubConnectionBuilder: class {
        withUrl() { return this; }
        withAutomaticReconnect() { return this; }
        build() { return connection; }
      },
    },
  });
  env.context.window.matchMedia = () => ({ matches: false });
  env.load("js/shared.js");
  env.load("player/player.js");
  // Finish initial connection setup before simulating a user gesture.
  await new Promise(setImmediate);
  return { ...env, events, messages, calls };
}

for (const gesture of ["click", "Enter"]) {
  test(`player: joining with ${gesture} enables answer SFX`, async () => {
    const { events, messages, calls, plays } = await loadPlayer();
    if (gesture === "click") await events.get("btn-join:click")();
    else await events.get("nickname:keydown")({ key: "Enter", preventDefault() {} });

    assert.equal(calls[0]?.[0], "JoinRoom");
    assert.deepEqual(Array.from(calls[0]), ["JoinRoom", "ABCD", "Tester"]);
    messages.get("AnswerAccepted")({ ok: true, error: null, points: 100 });
    assert.deepEqual(plays, [{ src: "/audio/sfx-correct.ogg", volume: 0.55 }]);
  });
}
