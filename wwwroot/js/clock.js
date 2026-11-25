(() => {
  const clockPage = document.querySelector("[data-clock-page]");
  if (!clockPage) {
    return;
  }

  const dateElement = document.getElementById("clock-date");
  const timeElement = document.getElementById("clock-time");
  const zoneElement = document.getElementById("clock-zone");
  const picker = document.querySelector("[data-timezone-picker]");
  const pickerButton = document.getElementById("timezone-button");
  const menu = document.getElementById("timezone-menu");
  const searchInput = document.getElementById("timezone-search");
  const optionsElement = document.getElementById("timezone-options");
  const emptyElement = document.getElementById("timezone-empty");

  const storageKey = "skilltest.selectedTimeZone";
  const fallbackTimeZone = "Asia/Taipei";
  const commonTimeZones = [
    { id: "Asia/Taipei", name: "台北" },
    { id: "Asia/Tokyo", name: "東京" },
    { id: "Asia/Seoul", name: "首爾" },
    { id: "Asia/Hong_Kong", name: "香港" },
    { id: "Asia/Singapore", name: "新加坡" },
    { id: "Asia/Bangkok", name: "曼谷" },
    { id: "Asia/Kolkata", name: "孟買" },
    { id: "Asia/Dubai", name: "杜拜" },
    { id: "Europe/London", name: "倫敦" },
    { id: "Europe/Paris", name: "巴黎" },
    { id: "America/New_York", name: "紐約" },
    { id: "America/Los_Angeles", name: "洛杉磯" },
    { id: "Australia/Sydney", name: "雪梨" },
    { id: "Pacific/Auckland", name: "奧克蘭" }
  ];

  const fallbackTimeZones = [
    "Africa/Cairo", "Africa/Johannesburg", "Africa/Lagos", "Africa/Nairobi",
    "America/Anchorage", "America/Argentina/Buenos_Aires", "America/Bogota", "America/Chicago",
    "America/Denver", "America/Halifax", "America/Honolulu", "America/Mexico_City",
    "America/Phoenix", "America/Sao_Paulo", "America/St_Johns", "America/Toronto",
    "Asia/Almaty", "Asia/Baghdad", "Asia/Beirut", "Asia/Dhaka", "Asia/Jerusalem",
    "Asia/Karachi", "Asia/Kathmandu", "Asia/Kuala_Lumpur", "Asia/Manila", "Asia/Riyadh",
    "Asia/Shanghai", "Asia/Tehran", "Asia/Tashkent", "Asia/Yangon",
    "Australia/Adelaide", "Australia/Brisbane", "Australia/Perth",
    "Europe/Amsterdam", "Europe/Athens", "Europe/Berlin", "Europe/Helsinki",
    "Europe/Istanbul", "Europe/Moscow", "Europe/Rome",
    "Pacific/Fiji", "Pacific/Guam", "Pacific/Honolulu", "Pacific/Port_Moresby",
    "UTC"
  ];

  const friendlyNames = new Map(commonTimeZones.map(({ id, name }) => [id, name]));
  const dateFormatters = new Map();
  const timeFormatters = new Map();
  const offsetFormatters = new Map();

  function isSupportedTimeZone(timeZone) {
    if (!timeZone) {
      return false;
    }

    try {
      new Intl.DateTimeFormat("en", { timeZone });
      return true;
    } catch {
      return false;
    }
  }

  function getSupportedTimeZones() {
    const browserTimeZones = typeof Intl.supportedValuesOf === "function"
      ? Intl.supportedValuesOf("timeZone")
      : fallbackTimeZones;

    return [...new Set([
      ...commonTimeZones.map(({ id }) => id),
      ...browserTimeZones
    ])].filter(isSupportedTimeZone);
  }

  function getSavedTimeZone() {
    try {
      return window.localStorage.getItem(storageKey);
    } catch {
      return null;
    }
  }

  function getDetectedTimeZone() {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch {
      return null;
    }
  }

  function getInitialTimeZone() {
    const savedTimeZone = getSavedTimeZone();
    if (isSupportedTimeZone(savedTimeZone)) {
      return savedTimeZone;
    }

    const detectedTimeZone = getDetectedTimeZone();
    if (isSupportedTimeZone(detectedTimeZone)) {
      return detectedTimeZone;
    }

    return fallbackTimeZone;
  }

  function getDisplayName(timeZone) {
    if (friendlyNames.has(timeZone)) {
      return friendlyNames.get(timeZone);
    }

    const segments = timeZone.split("/");
    return segments[segments.length - 1].replaceAll("_", " ");
  }

  function getDateFormatter(timeZone) {
    if (!dateFormatters.has(timeZone)) {
      dateFormatters.set(timeZone, new Intl.DateTimeFormat("en-CA", {
        timeZone,
        year: "numeric",
        month: "2-digit",
        day: "2-digit"
      }));
    }

    return dateFormatters.get(timeZone);
  }

  function getTimeFormatter(timeZone) {
    if (!timeFormatters.has(timeZone)) {
      timeFormatters.set(timeZone, new Intl.DateTimeFormat("en-GB", {
        timeZone,
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hourCycle: "h23"
      }));
    }

    return timeFormatters.get(timeZone);
  }

  function getOffsetFormatter(timeZone) {
    if (!offsetFormatters.has(timeZone)) {
      offsetFormatters.set(timeZone, new Intl.DateTimeFormat("en", {
        timeZone,
        timeZoneName: "shortOffset"
      }));
    }

    return offsetFormatters.get(timeZone);
  }

  function getPart(parts, type) {
    return parts.find((part) => part.type === type)?.value ?? "";
  }

  function getOffsetLabel(timeZone, now) {
    const rawOffset = getPart(getOffsetFormatter(timeZone).formatToParts(now), "timeZoneName");
    const match = /^(?:GMT|UTC)(?:([+-])(\d{1,2})(?::(\d{2}))?)?$/.exec(rawOffset);

    if (!match) {
      return rawOffset.replace(/^GMT/, "UTC");
    }

    if (!match[1]) {
      return "UTC+0";
    }

    const minutes = match[3] && match[3] !== "00" ? `:${match[3]}` : "";
    return `UTC${match[1]}${Number(match[2])}${minutes}`;
  }

  const timeZones = getSupportedTimeZones();
  let selectedTimeZone = getInitialTimeZone();
  let lastZoneLabel = "";
  let lastRenderedMinute = -1;

  function closeMenu(returnFocus = false) {
    menu.hidden = true;
    pickerButton.setAttribute("aria-expanded", "false");
    if (returnFocus) {
      pickerButton.focus();
    }
  }

  function createZoneOption(timeZone, now) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "timezone-option";
    button.setAttribute("aria-pressed", String(timeZone === selectedTimeZone));
    button.dataset.timeZone = timeZone;

    const name = document.createElement("span");
    name.className = "timezone-option__name";
    name.textContent = getDisplayName(timeZone);

    const details = document.createElement("span");
    details.className = "timezone-option__details";
    details.textContent = `${getOffsetLabel(timeZone, now)} · ${timeZone}`;

    button.append(name, details);
    button.addEventListener("click", () => {
      selectedTimeZone = timeZone;
      try {
        window.localStorage.setItem(storageKey, selectedTimeZone);
      } catch {
        // Keep the clock usable when browser storage is disabled.
      }

      updateClock();
      closeMenu(true);
    });

    return button;
  }

  function createOptionGroup(title, zones, now) {
    if (zones.length === 0) {
      return null;
    }

    const group = document.createElement("section");
    group.className = "timezone-option-group";

    const heading = document.createElement("h2");
    heading.className = "timezone-option-group__title";
    heading.textContent = title;
    group.append(heading);

    for (const timeZone of zones) {
      group.append(createZoneOption(timeZone, now));
    }

    return group;
  }

  function renderTimeZoneOptions() {
    const now = new Date();
    const query = searchInput.value.trim().toLocaleLowerCase("zh-Hant");
    const commonIds = commonTimeZones.map(({ id }) => id);
    const commonIdSet = new Set(commonIds);
    const matches = timeZones.filter((timeZone) => {
      if (query.length === 0) {
        return true;
      }

      const searchableText = `${getDisplayName(timeZone)} ${timeZone} ${getOffsetLabel(timeZone, now)}`;
      return searchableText.toLocaleLowerCase("zh-Hant").includes(query);
    });

    const fragment = document.createDocumentFragment();
    if (query.length > 0) {
      const group = createOptionGroup("搜尋結果", matches, now);
      if (group) {
        fragment.append(group);
      }
    } else {
      const matchingCommon = commonIds.filter((timeZone) => matches.includes(timeZone));
      const matchingOthers = matches
        .filter((timeZone) => !commonIdSet.has(timeZone))
        .sort((left, right) => getDisplayName(left).localeCompare(getDisplayName(right), "zh-Hant"));

      const commonGroup = createOptionGroup("常用時區", matchingCommon, now);
      const otherGroup = createOptionGroup("所有時區", matchingOthers, now);
      if (commonGroup) {
        fragment.append(commonGroup);
      }
      if (otherGroup) {
        fragment.append(otherGroup);
      }
    }

    optionsElement.replaceChildren(fragment);
    emptyElement.hidden = matches.length > 0;
    lastRenderedMinute = Math.floor(now.getTime() / 60000);
  }

  function updateClock() {
    const now = new Date();
    const dateParts = getDateFormatter(selectedTimeZone).formatToParts(now);
    const timeParts = getTimeFormatter(selectedTimeZone).formatToParts(now);
    const dateText = `${getPart(dateParts, "year")}/${getPart(dateParts, "month")}/${getPart(dateParts, "day")}`;
    const timeText = `${getPart(timeParts, "hour")}:${getPart(timeParts, "minute")}:${getPart(timeParts, "second")}`;
    const zoneLabel = `${getDisplayName(selectedTimeZone)} · ${getOffsetLabel(selectedTimeZone, now)} · ${selectedTimeZone}`;

    dateElement.textContent = dateText;
    timeElement.textContent = timeText;
    timeElement.dateTime = `${dateText}T${timeText}`;
    if (zoneLabel !== lastZoneLabel) {
      zoneElement.textContent = zoneLabel;
      lastZoneLabel = zoneLabel;
    }

    const currentMinute = Math.floor(now.getTime() / 60000);
    if (!menu.hidden && currentMinute !== lastRenderedMinute) {
      renderTimeZoneOptions();
    }
  }

  pickerButton.addEventListener("click", () => {
    if (!menu.hidden) {
      closeMenu();
      return;
    }

    searchInput.value = "";
    renderTimeZoneOptions();
    menu.hidden = false;
    pickerButton.setAttribute("aria-expanded", "true");
    window.requestAnimationFrame(() => searchInput.focus());
  });

  searchInput.addEventListener("input", renderTimeZoneOptions);
  picker.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !menu.hidden) {
      closeMenu(true);
    }
  });

  document.addEventListener("pointerdown", (event) => {
    if (!picker.contains(event.target)) {
      closeMenu();
    }
  });

  updateClock();
  window.setInterval(updateClock, 1000);
})();
