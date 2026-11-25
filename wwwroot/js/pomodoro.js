(() => {
  const page = document.querySelector("[data-pomodoro]");
  if (!page) {
    return;
  }

  const storageKey = "skilltest.pomodoro";
  const defaults = { focusMinutes: 25, shortBreakMinutes: 5, longBreakMinutes: 20 };
  const settings = {
    focusMinutes: document.getElementById("focus-duration"),
    shortBreakMinutes: document.getElementById("short-break-duration"),
    longBreakMinutes: document.getElementById("long-break-duration")
  };
  const focusItem = document.getElementById("focus-item");
  const phaseElement = document.getElementById("pomodoro-phase");
  const remainingElement = document.getElementById("pomodoro-remaining");
  const completedElement = document.getElementById("pomodoro-completed");
  const timerMessageElement = document.getElementById("timer-message");
  const muteSoundInput = document.getElementById("mute-sound");
  const desktopNotificationsInput = document.getElementById("desktop-notifications");
  const notificationStatusElement = document.getElementById("notification-status");
  const startButton = document.getElementById("focus-start");
  const pauseButton = document.getElementById("focus-pause");
  const resumeButton = document.getElementById("focus-resume");
  const resetFocusButton = document.getElementById("focus-reset");
  const resetCycleButton = document.getElementById("cycle-reset");

  function isPositiveInteger(value) {
    return Number.isSafeInteger(value) && value > 0;
  }

  function readStoredState() {
    try {
      const storedState = JSON.parse(window.localStorage.getItem(storageKey) ?? "null");
      return storedState && typeof storedState === "object" && !Array.isArray(storedState)
        ? storedState
        : {};
    } catch {
      return {};
    }
  }

  const storedState = readStoredState();
  const storedSettings = storedState.settings && typeof storedState.settings === "object"
    ? storedState.settings
    : {};
  const currentSettings = { ...defaults };

  for (const [key, input] of Object.entries(settings)) {
    if (isPositiveInteger(storedSettings[key])) {
      currentSettings[key] = storedSettings[key];
    }
    input.value = String(currentSettings[key]);
  }

  const storedPhase = ["focus", "shortBreak", "longBreak"].includes(storedState.phase)
    ? storedState.phase
    : "focus";
  const storedStatus = storedState.status === "running" || storedState.status === "paused"
    ? storedState.status
    : "idle";
  const savedRemainingMs = Number.isFinite(storedState.remainingMs) && storedState.remainingMs >= 0
    ? storedState.remainingMs
    : currentSettings.focusMinutes * 60_000;
  const storedDeadline = Number.isFinite(storedState.deadlineMs) ? storedState.deadlineMs : null;
  const restoredStatus = storedPhase === "focus" && storedStatus === "running"
    ? "paused"
    : storedPhase !== "focus" && storedStatus !== "idle"
      ? "running"
      : storedStatus;
  const restoredRemainingMs = storedStatus === "running" && storedDeadline !== null
    ? Math.max(0, storedDeadline - Date.now())
    : savedRemainingMs;
  const state = {
    settings: currentSettings,
    focusItem: typeof storedState.focusItem === "string" ? storedState.focusItem : "",
    phase: storedPhase,
    status: restoredStatus,
    remainingMs: restoredRemainingMs,
    deadlineMs: restoredStatus === "running" && storedPhase !== "focus"
      ? Date.now() + restoredRemainingMs
      : null,
    phaseDurationMs: Number.isFinite(storedState.phaseDurationMs) && storedState.phaseDurationMs > 0
      ? storedState.phaseDurationMs
      : currentSettings.focusMinutes * 60_000,
    completedPomodoros: Number.isInteger(storedState.completedPomodoros)
      ? Math.min(4, Math.max(0, storedState.completedPomodoros))
      : 0
  };
  state.muted = storedState.muted === true;
  state.desktopNotificationsEnabled = storedState.desktopNotificationsEnabled === true;
  let audioContext = null;
  focusItem.value = state.focusItem;

  function getRemainingMs() {
    if (state.status === "running" && state.deadlineMs !== null) {
      return Math.max(0, state.deadlineMs - Date.now());
    }

    return state.remainingMs;
  }

  function formatRemainingTime(milliseconds) {
    const totalSeconds = Math.ceil(milliseconds / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  }

  function saveState() {
    state.remainingMs = getRemainingMs();
    try {
      window.localStorage.setItem(storageKey, JSON.stringify({
        ...state,
        settings: { ...state.settings },
        deadlineMs: state.status === "running" ? state.deadlineMs : null
      }));
    } catch {
      // Keep the timer usable when browser storage is disabled.
    }
  }

  function prepareAudio() {
    if (state.muted) {
      return;
    }

    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextType) {
      return;
    }

    try {
      audioContext ??= new AudioContextType();
      if (audioContext.state === "suspended") {
        audioContext.resume().catch(() => {});
      }
    } catch {
      audioContext = null;
    }
  }

  function playSound() {
    if (state.muted) {
      return;
    }

    prepareAudio();
    if (!audioContext) {
      return;
    }

    try {
      const oscillator = audioContext.createOscillator();
      const gain = audioContext.createGain();
      const startAt = audioContext.currentTime;
      oscillator.type = "sine";
      oscillator.frequency.setValueAtTime(660, startAt);
      gain.gain.setValueAtTime(0.08, startAt);
      gain.gain.exponentialRampToValueAtTime(0.001, startAt + 0.2);
      oscillator.connect(gain);
      gain.connect(audioContext.destination);
      oscillator.start(startAt);
      oscillator.stop(startAt + 0.2);
    } catch {
      // Keep timer transitions working when browser audio is unavailable.
    }
  }

  function sendDesktopNotification(message) {
    if (!state.desktopNotificationsEnabled || !("Notification" in window)
      || window.Notification.permission !== "granted") {
      return;
    }

    try {
      new window.Notification("番茄鐘", { body: `${message}。` });
    } catch {
      // Keep the in-page prompt available when desktop notifications fail.
    }
  }

  function render() {
    if (state.phase === "focus") {
      phaseElement.textContent = state.status === "running"
        ? "專注中"
        : state.status === "paused"
          ? "專注已暫停"
          : state.message || "準備開始專注";
    } else {
      phaseElement.textContent = state.phase === "shortBreak" ? "短休息中" : "長休息中";
    }
    remainingElement.textContent = formatRemainingTime(getRemainingMs());
    completedElement.textContent = String(state.completedPomodoros);
    timerMessageElement.textContent = state.message ? `${state.message}。` : "";
    muteSoundInput.checked = state.muted;
    desktopNotificationsInput.checked = state.desktopNotificationsEnabled;
    const isFocus = state.phase === "focus";
    focusItem.readOnly = isFocus && (state.status === "running" || state.status === "paused");
    startButton.hidden = !isFocus || state.status !== "idle";
    pauseButton.hidden = !isFocus || state.status !== "running";
    resumeButton.hidden = !isFocus || state.status !== "paused";
    resetFocusButton.hidden = !isFocus || state.status === "idle";
    resetCycleButton.hidden = !isFocus || (state.completedPomodoros === 0 && state.status === "idle");
  }

  function startFocus() {
    if (state.phase !== "focus") {
      return;
    }

    if (state.status === "idle") {
      state.phaseDurationMs = state.settings.focusMinutes * 60_000;
      state.remainingMs = state.phaseDurationMs;
    }

    state.deadlineMs = Date.now() + state.remainingMs;
    state.status = "running";
    state.message = "";
    prepareAudio();
    saveState();
    render();
  }

  function completePhase() {
    if (state.phase === "focus") {
      state.completedPomodoros = Math.min(4, state.completedPomodoros + 1);
      state.phase = state.completedPomodoros === 4 ? "longBreak" : "shortBreak";
      const durationKey = state.phase === "longBreak" ? "longBreakMinutes" : "shortBreakMinutes";
      state.phaseDurationMs = state.settings[durationKey] * 60_000;
      state.remainingMs = state.phaseDurationMs;
      state.deadlineMs = Date.now() + state.remainingMs;
      state.status = "running";
      state.message = state.phase === "longBreak"
        ? "專注時間結束，開始長休息"
        : "專注時間結束，開始短休息";
    } else {
      const completedPhase = state.phase;
      if (completedPhase === "longBreak") {
        state.completedPomodoros = 0;
      }
      state.phase = "focus";
      state.phaseDurationMs = state.settings.focusMinutes * 60_000;
      state.remainingMs = state.phaseDurationMs;
      state.deadlineMs = null;
      state.status = "idle";
      state.message = completedPhase === "longBreak"
        ? "長休息結束，新循環已開始"
        : "短休息結束，準備開始下一段專注";
    }

    playSound();
    sendDesktopNotification(state.message);
    saveState();
    render();
  }

  function updateTimer() {
    if (state.status === "running" && getRemainingMs() <= 0) {
      completePhase();
      return;
    }
    render();
  }

  focusItem.addEventListener("input", () => {
    state.focusItem = focusItem.value;
    saveState();
  });

  for (const [key, input] of Object.entries(settings)) {
    input.addEventListener("input", () => {
      const value = Number(input.value);
      if (!input.validity.valid || !isPositiveInteger(value)) {
        return;
      }

      state.settings[key] = value;
      if (state.phase === "focus" && state.status === "idle") {
        state.phaseDurationMs = state.settings.focusMinutes * 60_000;
        state.remainingMs = state.phaseDurationMs;
      }
      saveState();
      render();
    });
  }

  startButton.addEventListener("click", startFocus);
  pauseButton.addEventListener("click", () => {
    state.remainingMs = getRemainingMs();
    state.deadlineMs = null;
    state.status = "paused";
    saveState();
    render();
  });
  resumeButton.addEventListener("click", startFocus);
  muteSoundInput.addEventListener("change", () => {
    state.muted = muteSoundInput.checked;
    if (!state.muted) {
      prepareAudio();
    }
    saveState();
    render();
  });
  desktopNotificationsInput.addEventListener("change", async () => {
    if (!desktopNotificationsInput.checked) {
      state.desktopNotificationsEnabled = false;
      notificationStatusElement.textContent = "已關閉桌面通知。";
      saveState();
      render();
      return;
    }

    if (!("Notification" in window)) {
      state.desktopNotificationsEnabled = false;
      notificationStatusElement.textContent = "此瀏覽器不支援桌面通知。";
      saveState();
      render();
      return;
    }

    try {
      const permission = window.Notification.permission === "default"
        ? await window.Notification.requestPermission()
        : window.Notification.permission;
      state.desktopNotificationsEnabled = permission === "granted";
      notificationStatusElement.textContent = state.desktopNotificationsEnabled
        ? "已啟用桌面通知。"
        : "瀏覽器未授予桌面通知權限。";
    } catch {
      state.desktopNotificationsEnabled = false;
      notificationStatusElement.textContent = "無法啟用桌面通知。";
    }

    saveState();
    render();
  });
  resetFocusButton.addEventListener("click", () => {
    state.remainingMs = state.phaseDurationMs;
    state.deadlineMs = null;
    state.status = "idle";
    saveState();
    render();
  });
  resetCycleButton.addEventListener("click", () => {
    if (state.phase !== "focus" || !window.confirm("確定要重設目前循環嗎？")) {
      return;
    }

    state.completedPomodoros = 0;
    state.status = "idle";
    state.phaseDurationMs = state.settings.focusMinutes * 60_000;
    state.remainingMs = state.phaseDurationMs;
    state.deadlineMs = null;
    state.message = "";
    saveState();
    render();
  });

  window.addEventListener("pagehide", () => {
    if (state.status !== "running") {
      return;
    }

    state.remainingMs = getRemainingMs();
    state.deadlineMs = null;
    state.status = "paused";
    saveState();
  });

  state.message = typeof storedState.message === "string" ? storedState.message : "";
  window.setInterval(updateTimer, 250);
  render();
})();
