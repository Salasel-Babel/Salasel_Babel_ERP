/* Web-app keys — Hindi (LTR, Devanagari).
   ⚠ TRANSLATION QUALITY: MACHINE-GRADE PLACEHOLDER, same standard as hi.base.ts.
   Note the plural trap this language exists to catch:
   Intl.PluralRules("hi").select(0) === "one", so an English-trained
   `count === 1 ? a : b` is wrong here at zero. The "=0" form covers it. */
import type { MessageTree } from "../types";

export const messages: MessageTree = {
  app: {
    web: {
      docTitle: "सलासिल बाबिल — तलपट",
      tagline: "मूलतः अरबी लेखांकन · प्रकाशित अनुबंध से बैकएंड से पूर्णतः पृथक फ़्रंटएंड",
      skipToTable: "तालिका पर जाएँ",
    },
    health: {
      label: "सेवा की स्थिति",
      ok: "जुड़ा हुआ",
      down: "कोई उत्तर नहीं",
      checking: "जाँच जारी",
      culture: "सर्वर की संस्कृति",
      calendar: "सर्वर का पंचांग",
      apiVersion: "सतह का संस्करण",
      hijriWarning: "सर्वर उम्म अल-क़ुरा पंचांग पर चल रहा है — वहाँ कोई भी अंतर्निहित तिथि हिजरी में लिखी जाएगी।",
    },
    nav: { contract: "प्रकाशित अनुबंध" },
  },
  common: {
    state: {
      loading: "लोड हो रहा है",
      loadingBody: "तलपट सर्वर से पढ़ा जा रहा है। आँकड़ों में कुछ नहीं बदलता।",
    },
    action: {
      clearFilters: "फ़िल्टर हटाएँ",
      keyboardHelp: "कीबोर्ड शॉर्टकट",
    },
    problem: {
      title: "अनुरोध पूरा नहीं हुआ",
      code: "कोड",
      trace: "ट्रेस पहचान",
      field: "क्षेत्र",
      status: "स्थिति कोड",
      noContract: "सर्वर ने प्रकाशित समस्या-प्रारूप में उत्तर नहीं दिया — न कोड, न अरबी संदेश।",
      network: "सर्वर तक नहीं पहुँचा जा सका। आँकड़ों में कुछ नहीं बदला।",
      count: {
        "=0": "कोई त्रुटि नहीं",
        one: "एक त्रुटि",
        other: "{count} त्रुटियाँ",
      },
    },
    keys: {
      title: "कीबोर्ड शॉर्टकट",
      hint: "शॉर्टकट के लिए ? दबाएँ",
      search: "खोज पर जाएँ",
      rowNext: "अगली पंक्ति",
      rowPrev: "पिछली पंक्ति",
      rowFirst: "पहली पंक्ति",
      rowLast: "अंतिम पंक्ति",
      pageNext: "दस पंक्तियाँ आगे",
      pagePrev: "दस पंक्तियाँ पीछे",
      viewCycle: "दृश्य बदलें: सभी · नामे · जमा",
      reload: "तलपट फिर से पढ़ें",
      help: "यह सूची दिखाएँ",
      dismiss: "बंद करें, या खोज हटाएँ",
    },
  },
  field: {
    book: { label: "बही", hint: "कंपनी के भीतर की बही, जैसे MAIN" },
    company: { label: "कंपनी", hint: "कंपनी पहचान — दायरा प्रमाण-पत्र से मिलाया जाता है" },
    periodCode: {
      label: "अवधि कोड",
      hint: "ग्रेगोरियन yyyy-MM, या सभी अवधियों के लिए खाली छोड़ें",
      bad: "अवधि कोड प्रकाशित प्रारूप yyyy-MM से मेल नहीं खाता",
      all: "सभी अवधियाँ",
    },
    token: { label: "प्रमाण-पत्र", hint: "पहचान केवल प्रमाण-पत्र से आती है — कोई टेनेंट हेडर नहीं" },
  },
  screen: {
    trialBalance: {
      sourceNote: "पंक्तियाँ अपरिवर्तनीय जर्नल पंक्तियों से आती हैं, किसी शेष-तालिका से नहीं।",
      totalsNote: "दोनों योग उसी क्वेरी में numeric पर sum() से निकाले जाते हैं — ब्राउज़र में कभी नहीं।",
      sortedBy: "{column} के अनुसार क्रमित",
      matching: {
        "=0": "कोई मेल खाता खाता नहीं",
        one: "एक मेल खाता खाता",
        other: "{count} मेल खाते खाते",
      },
    },
    contract: {
      title: "प्रकाशित अनुबंध",
      sub: "फ़्रंटएंड को जो कुछ चाहिए। यह कोई बैकएंड कोड नहीं पढ़ता।",
      version: "अनुबंध संस्करण",
      digest: "अनुबंध की छाप",
      note: "प्रकार और क्लाइंट इसी फ़ाइल से उत्पन्न होते हैं। इसका बदलना निर्माण को ऊँची आवाज़ में तोड़ता है, चुपचाप नहीं।",
      moneyNote: "राशि तार पर पाठ है, और यहाँ उसका प्रकार एक वस्तु है जो संख्या में बदलने पर त्रुटि फेंकती है।",
      operations: {
        "=0": "कोई संक्रिया नहीं",
        one: "एक संक्रिया",
        other: "{count} संक्रियाएँ",
      },
      schemas: {
        "=0": "कोई स्कीमा नहीं",
        one: "एक स्कीमा",
        other: "{count} स्कीमा",
      },
    },
  },
};
