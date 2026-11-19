(() => {
  const STORAGE_KEY = "storespos.login.lang";
  const page = document.getElementById("loginPage");
  const toggle = document.getElementById("loginLangToggle");
  if (!page || !toggle) {
    return;
  }

  const copy = {
    en: {
      brand: "StoresPOS",
      brandSub: "Local store cashier",
      eyebrow: "Secure sign-in",
      title: "Welcome back",
      subtitle: "Sign in to open the till and continue store operations.",
      username: "Username",
      password: "Password",
      usernamePlaceholder: "Enter username",
      passwordPlaceholder: "Enter password",
      submit: "Sign in",
      footer: "StoresPOS · Local installation",
      pageTitle: "Login - StoresPOS",
      toggle: "العربية",
      toggleAria: "Switch to Arabic",
      dir: "ltr",
      lang: "en"
    },
    ar: {
      brand: "StoresPOS",
      brandSub: "نقطة البيع المحلية",
      eyebrow: "تسجيل دخول آمن",
      title: "مرحبًا بعودتك",
      subtitle: "سجّل الدخول لفتح الوردية ومتابعة عمليات المتجر.",
      username: "اسم المستخدم",
      password: "كلمة المرور",
      usernamePlaceholder: "أدخل اسم المستخدم",
      passwordPlaceholder: "أدخل كلمة المرور",
      submit: "تسجيل الدخول",
      footer: "StoresPOS · تثبيت محلي",
      pageTitle: "تسجيل الدخول - StoresPOS",
      toggle: "English",
      toggleAria: "التبديل إلى الإنجليزية",
      dir: "rtl",
      lang: "ar"
    }
  };

  const apply = (locale) => {
    const t = copy[locale] || copy.en;

    page.setAttribute("dir", t.dir);
    page.setAttribute("lang", t.lang);
    document.documentElement.setAttribute("dir", t.dir);
    document.documentElement.setAttribute("lang", t.lang);
    document.title = t.pageTitle;

    toggle.dataset.lang = locale;
    toggle.textContent = t.toggle;
    toggle.setAttribute("aria-label", t.toggleAria);

    page.querySelectorAll("[data-i18n]").forEach((el) => {
      const key = el.getAttribute("data-i18n");
      if (key && Object.prototype.hasOwnProperty.call(t, key)) {
        el.textContent = t[key];
      }
    });

    page.querySelectorAll("[data-i18n-placeholder]").forEach((el) => {
      const key = el.getAttribute("data-i18n-placeholder");
      if (key && Object.prototype.hasOwnProperty.call(t, key)) {
        el.setAttribute("placeholder", t[key]);
      }
    });

    // Share the choice with the server (UI language for the app; formatting stays en-US).
    document.cookie = ".AspNetCore.Culture=" + encodeURIComponent("c=en-US|uic=" + t.lang)
      + "; path=/; max-age=31536000; samesite=strict";

    try {
      localStorage.setItem(STORAGE_KEY, locale);
    } catch {
      // Ignore storage failures in restricted environments.
    }
  };

  const saved = (() => {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  })();

  const initial = saved === "ar" || saved === "en" ? saved : "en";
  apply(initial);

  toggle.addEventListener("click", () => {
    const next = toggle.dataset.lang === "ar" ? "en" : "ar";
    apply(next);
  });
})();
