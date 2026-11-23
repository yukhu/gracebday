/* ---------------------------------------------------------
   A little birthday card — interactions
   --------------------------------------------------------- */

(() => {
  "use strict";

  const spread = document.getElementById("spread");
  const cover = document.getElementById("cover");
  const inside = document.getElementById("inside");
  const confettiLayer = document.getElementById("confetti");
  const cardAudio = document.getElementById("cardAudio");
  const audioSource = cardAudio.querySelector("source");

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const CONFETTI_COLORS = [
    "#e25f4e",
    "#c84463",
    "#eea63c",
    "#7fb3a1",
    "#e88fa0",
    "#f5d6a4",
    "#b7c8e6",
  ];

  let isOpen = false;

  function setOpen(open, opts = {}) {
    if (open === isOpen) return;
    isOpen = open;

    spread.classList.toggle("is-open", open);
    cover.setAttribute("aria-expanded", String(open));
    cover.setAttribute(
      "aria-label",
      open ? "Close the birthday card" : "Open the birthday card"
    );
    inside.inert = !open;

    if (open) {
      playMusic();
      if (opts.focusInside) {
        // wait for the flip so focus lands inside the revealed card
        window.setTimeout(() => inside.focus({ preventScroll: true }), 420);
      }
      if (!reduceMotion.matches) {
        window.setTimeout(spawnConfetti, 480);
      }
    } else {
      pauseMusic();
      if (opts.focusCover) {
        cover.focus({ preventScroll: true });
      }
    }
  }

  function playMusic() {
    // Resume from wherever playback was paused when the card last closed.
    const playback = cardAudio.play();
    if (playback) {
      // Autoplay policies can still reject; the card remains fully usable.
      playback.catch(() => {});
    }
  }

  function pauseMusic() {
    // Pause (rather than stop) so the track keeps its position.
    cardAudio.pause();
  }

  function warmAudioBuffer() {
    // Download the whole track once, at startup, and hand it to the media
    // element as an in-memory blob. The first tap can then start playback
    // without waiting on the network.
    const url = audioSource ? audioSource.src : cardAudio.src;

    fetch(url)
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Audio request failed: ${response.status}`);
        }
        return response.blob();
      })
      .then((blob) => {
        // If the card was already played, don't disturb the current playback.
        if (!cardAudio.paused || cardAudio.currentTime > 0) return;

        cardAudio.src = URL.createObjectURL(blob);
        cardAudio.load();
      })
      .catch(() => {
        // fetch() is unavailable in some contexts (e.g. the page opened
        // directly from disk), so fall back to normal media preloading.
        cardAudio.preload = "auto";
        cardAudio.load();
      });
  }

  warmAudioBuffer();

  /* ---------- startup: reveal the card once its artwork is ready ---------- */

  function preloadCardImage() {
    const link = document.querySelector('link[rel="preload"][as="image"]');

    return new Promise((resolve) => {
      const image = new Image();
      image.onload = () => {
        // decode() settles only once the image can be painted without a hitch.
        if (image.decode) {
          image.decode().then(resolve, resolve);
        } else {
          resolve();
        }
      };
      image.onerror = resolve; // if the art is missing, show the card anyway
      image.src = link ? link.href : "assets/card.jpg";
    });
  }

  function handleTap(event) {
    const fromKeyboard = event.detail === 0;
    if (isOpen) {
      setOpen(false, { focusCover: fromKeyboard });
    } else {
      setOpen(true, { focusInside: fromKeyboard });
    }
  }

  preloadCardImage().then(() => {
    document.body.classList.add("is-ready");
    // Only listen for taps once the card is actually visible.
    document.addEventListener("click", handleTap);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isOpen) {
      setOpen(false, { focusCover: true });
    }
  });

  /* ---------- confetti ---------- */

  function spawnConfetti() {
    if (reduceMotion.matches) return;

    const count = window.innerWidth < 520 ? 24 : 40;

    for (let i = 0; i < count; i++) {
      const piece = document.createElement("span");
      const size = 4 + Math.random() * 5;

      piece.className = "confetti-piece";
      piece.style.setProperty("--x", `${4 + Math.random() * 92}vw`);
      piece.style.setProperty("--delay", `${Math.random() * 0.45}s`);
      piece.style.setProperty("--dur", `${2.2 + Math.random() * 1.7}s`);
      piece.style.setProperty("--drift", `${(Math.random() - 0.5) * 26}vw`);
      piece.style.setProperty("--spin", `${Math.round(Math.random() * 720 - 360)}deg`);
      piece.style.setProperty("--size", `${size.toFixed(1)}px`);
      piece.style.background =
        CONFETTI_COLORS[Math.floor(Math.random() * CONFETTI_COLORS.length)];

      if (i % 3 === 0) {
        piece.style.borderRadius = "50%";
        piece.style.height = `${size.toFixed(1)}px`;
      }

      confettiLayer.appendChild(piece);
      piece.addEventListener("animationend", () => piece.remove(), { once: true });
    }
  }
})();
