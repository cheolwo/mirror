const roles = [
  {
    key: "community",
    eyebrow: "COMMUNITY · ORDERER",
    title: "주문자·커뮤니티",
    description: "동네 글을 읽고 공동구매나 공동수입 제안을 구체적인 업무 흐름으로 연결합니다.",
    image: "/assets/images/community-orderer.png",
    imageAlt: "모바일 Hongdal 생활 게시판 화면",
    accent: "blue",
    startPath: "/community",
    screens: [
      { path: "/community", title: "생활 게시판", description: "동네 글과 댓글, 연결된 업무 원장을 함께 봅니다.", tag: "바로 보기" },
      { path: "/community/write", title: "판매글 작성", description: "사진, 상품명, 수량, 가격과 결제 협의 방법을 게시글에 붙입니다.", tag: "모바일 체험" },
      { path: "/community/group-purchase", title: "공동구매 제안", description: "참여자와 수량, 가격, 공급 조건을 맞춥니다.", tag: "흐름 체험" },
      { path: "/community/group-import", title: "공동수입 검토", description: "해외 공급과 국내 수요를 수입 흐름으로 연결합니다.", tag: "흐름 체험" },
      { path: "/information/public-data", title: "농수산물 공개 정보", description: "공식 가격 자료와 출처를 비교해 봅니다.", tag: "바로 보기" }
    ]
  },
  {
    key: "global",
    eyebrow: "OVERSEAS SUPPLIER",
    title: "해외 공급자",
    description: "한국 시장에 상품을 소개하고 공급 조건과 수입 검토 가능성을 확인합니다.",
    image: "/assets/images/global-supplier.png",
    imageAlt: "Hongdal Global 상품 탐색 화면",
    accent: "teal",
    startPath: "/global",
    screens: [
      { path: "/global", title: "글로벌 상품", description: "한국 시장을 찾는 해외 상품과 공급 조건을 둘러봅니다.", tag: "바로 보기" },
      { path: "/global/suppliers/apply", title: "상품 제출", description: "회사, 상품, MOQ와 무역 조건 제출 흐름을 봅니다.", tag: "세션 체험" },
      { path: "/global/products/indonesian-rattan-storage-basket", title: "상품 상세", description: "샘플, HS 코드 제안과 수입 검토 정보를 확인합니다.", tag: "바로 보기" },
      { path: "/community/global-trade/101", title: "무역 대화", description: "공급자와 국내 참여자가 공개적으로 조건을 맞춥니다.", tag: "세션 체험" }
    ]
  },
  {
    key: "shipper",
    eyebrow: "SHIPPER · SELLER",
    title: "화주·판매자",
    description: "운송 의뢰부터 입고, 재고, 판매채널과 주문 출고까지 이어지는 업무를 봅니다.",
    image: "/assets/images/shipper-seller.png",
    imageAlt: "화주 역할 업무 선택 화면",
    accent: "green",
    startPath: "/shipper/request",
    screens: [
      { path: "/shipper/request", title: "운송 의뢰", description: "화물, 상하차지, 차량과 결제 조건을 입력합니다.", tag: "절차 체험" },
      { path: "/shipper/inbound/dashboard", title: "입고 대시보드", description: "입고 예정과 완료, 보관 재고를 한눈에 봅니다.", tag: "화면 체험" },
      { path: "/shipper/sales/pages/new", title: "판매 페이지 만들기", description: "판매자 유형과 주문 방식을 정하고 상품 상세 자료로 초안을 만듭니다.", tag: "모바일 체험" },
      { path: "/shipper/sales/channels", title: "판매채널", description: "판매채널 계정과 연결 상태를 관리합니다.", tag: "화면 체험" },
      { path: "/shipper/international/fcl-lcl", title: "통관·FCL/LCL", description: "수입량과 비용을 비교하고 운송 방식을 검토합니다.", tag: "화면 체험" }
    ]
  },
  {
    key: "driver",
    eyebrow: "CARGO DRIVER",
    title: "운송 기사",
    description: "추천 운송을 지도에서 확인하고 상차, 하차, 증빙과 정산을 처리합니다.",
    image: "/assets/images/driver.png",
    imageAlt: "운송 기사 추천 경로 지도 화면",
    accent: "cyan",
    startPath: "/driver/home",
    screens: [
      { path: "/driver/home", title: "기사 홈", description: "현재 위치와 진행 중 운송을 중심으로 봅니다.", tag: "화면 체험" },
      { path: "/driver/recommendations", title: "추천 운송", description: "거리, 예상 수익과 상하차 조건을 비교합니다.", tag: "절차 체험" },
      { path: "/driver/transports/current", title: "진행 중 운송", description: "수락한 운송의 현재 단계와 다음 행동을 확인합니다.", tag: "절차 체험" },
      { path: "/driver/transport/proof", title: "운송 증빙", description: "상차와 하차 사진, 예외 증빙 흐름을 확인합니다.", tag: "화면 체험" }
    ]
  },
  {
    key: "warehouse",
    eyebrow: "WAREHOUSE OPERATOR",
    title: "창고 관리자",
    description: "입고 확인, 검수, 적재, 피킹과 포장을 현장 순서대로 처리합니다.",
    image: "/assets/images/warehouse.png",
    imageAlt: "창고 관리자 역할 업무 선택 화면",
    accent: "olive",
    startPath: "/warehouse/work-board",
    screens: [
      { path: "/warehouse/work-board", title: "작업 보드", description: "입고와 출고 작업, 연결 대상을 한곳에서 봅니다.", tag: "화면 체험" },
      { path: "/warehouse/work/inbound/products", title: "입고 상품 확인", description: "상품 바코드로 입고 예정 항목을 찾습니다.", tag: "스캔 체험" },
      { path: "/warehouse/work/inbound/inspection", title: "입고 검수", description: "수량 차이, 파손과 보관 조건을 확인합니다.", tag: "절차 체험" },
      { path: "/warehouse/mart/picking", title: "마트 피킹·포장", description: "전달 주문을 피킹하고 포장 완료까지 처리합니다.", tag: "절차 체험" }
    ]
  }
];

const screenByPath = new Map();
for (const role of roles) {
  for (const screen of role.screens) {
    screenByPath.set(screen.path, { ...screen, role });
  }
}

const roleSamples = {
  community: [
    ["새 공동구매", "여름 제철 복숭아 공동구매", "참여 18명"],
    ["공동수입", "포르투갈 코르크 생활용품", "검토 중"],
    ["생활 질문", "동네 배송 묶음 제안", "댓글 12개"]
  ],
  global: [
    ["Portugal", "Modular Cork Desk Organizer", "MOQ 120"],
    ["Indonesia", "Handwoven Rattan Storage Basket", "MOQ 80"],
    ["Mexico", "Recycled Glass Table Vase", "MOQ 144"]
  ],
  shipper: [
    ["운송 준비", "서울 성동 → 부산 강서", "1.2톤"],
    ["입고 예정", "생활용품 24 SKU", "오늘 15:30"],
    ["판매채널", "주문 동기화", "정상"]
  ],
  driver: [
    ["추천 1", "4.8 km · 예상 42분", "예상 58,000원"],
    ["추천 2", "7.1 km · 예상 55분", "예상 72,000원"],
    ["진행 중", "상차지 도착 확인", "다음: 상차"]
  ],
  warehouse: [
    ["입고", "HD-IB-260717-14", "검수 대기"],
    ["피킹", "마트 주문 8건", "진행 중"],
    ["출고", "택배 인계 12박스", "16:00 마감"]
  ]
};

let selectedRoleKey = "community";
let toastTimer;

const app = document.querySelector("#app");
const toast = document.querySelector("#toast");
const dialog = document.querySelector("#demo-dialog");
const dialogCopy = document.querySelector("#demo-dialog-copy");

function roleLabel(role) {
  return `${role.title} 화면`;
}

function screenLink(screen, index) {
  const number = String(index + 1).padStart(2, "0");
  return `
    <a class="screen-link" href="${screen.path}" data-route>
      <span class="screen-link__number">${number}</span>
      <span class="screen-link__copy">
        <strong>${screen.title}</strong>
        <small>${screen.description}</small>
      </span>
      <span class="screen-link__tag">${screen.tag}</span>
      <span class="screen-link__arrow" aria-hidden="true">→</span>
    </a>`;
}

function renderHome() {
  const role = roles.find((item) => item.key === selectedRoleKey) || roles[0];
  document.title = "Hongdal 역할별 화면 체험";
  app.innerHTML = `
    <section class="experience experience--${role.accent}">
      <header class="experience__intro">
        <div>
          <span class="eyebrow">HONGDAL PRODUCT PREVIEW</span>
          <h1>역할별 화면 체험</h1>
          <p>현재 구현된 주요 흐름을 역할에 따라 열어보고, 실제 데이터 변경 없이 화면 동작을 살펴봅니다.</p>
        </div>
        <span class="read-only-badge">읽기 전용 · 5개 역할</span>
      </header>

      <nav class="role-selector" aria-label="체험할 역할 선택">
        ${roles.map((item) => `
          <button class="role-button ${item.key === role.key ? "is-selected" : ""}"
                  type="button"
                  data-role-key="${item.key}"
                  aria-pressed="${item.key === role.key}">
            <span class="role-button__index">${String(roles.indexOf(item) + 1).padStart(2, "0")}</span>
            ${item.title}
          </button>`).join("")}
      </nav>

      <div class="experience__workspace">
        <figure class="screen-preview">
          <img src="${role.image}" alt="${role.imageAlt}">
          <figcaption>
            <span>${role.eyebrow}</span>
            <strong>${role.title}</strong>
          </figcaption>
        </figure>

        <section class="role-detail" aria-label="${roleLabel(role)}">
          <header class="role-detail__header">
            <div>
              <span class="eyebrow">${role.eyebrow}</span>
              <h2>${role.title}</h2>
              <p>${role.description}</p>
            </div>
            <a class="primary-button" href="${role.startPath}" data-route>대표 화면 열기 <span aria-hidden="true">→</span></a>
          </header>
          <div class="screen-list">
            ${role.screens.map(screenLink).join("")}
          </div>
        </section>
      </div>

      <section class="preview-policy" aria-label="체험 범위">
        <div>
          <span class="eyebrow">SAFE PREVIEW</span>
          <h2>실제 업무를 바꾸지 않는 화면 체험</h2>
        </div>
        <p>로그인, 주문 확정, 배차 수락, 파일 업로드와 결제는 샘플 상태로만 반응합니다. 화면 이동과 버튼 동작은 공개 사이트 안에서 끝납니다.</p>
      </section>
    </section>`;

  bindHomeInteractions();
}

function renderScreen(entry) {
  const { role, title, description, tag, path } = entry;
  const samples = roleSamples[role.key];
  document.title = `${title} | Hongdal 체험`;
  app.innerHTML = `
    <section class="screen-page screen-page--${role.accent}">
      <nav class="breadcrumbs" aria-label="현재 위치">
        <a href="/" data-route>체험 홈</a>
        <span aria-hidden="true">/</span>
        <button type="button" data-select-role="${role.key}">${role.title}</button>
        <span aria-hidden="true">/</span>
        <strong>${title}</strong>
      </nav>

      <div class="screen-page__layout">
        <aside class="role-rail" aria-label="${role.title} 화면 목록">
          <span class="eyebrow">${role.eyebrow}</span>
          <h1>${role.title}</h1>
          <p>${role.description}</p>
          <nav>
            ${role.screens.map((screen) => `
              <a href="${screen.path}" data-route class="${screen.path === path ? "is-current" : ""}">
                <span>${screen.title}</span>
                <small>${screen.tag}</small>
              </a>`).join("")}
          </nav>
          <a class="secondary-button" href="/" data-route>다른 역할 보기</a>
        </aside>

        <article class="workbench">
          <header class="workbench__header">
            <div>
              <span class="eyebrow">${tag}</span>
              <h2>${title}</h2>
              <p>${description}</p>
            </div>
            <button class="primary-button" type="button" data-demo-action="${title}">체험 동작 실행</button>
          </header>

          <section class="workflow" aria-labelledby="workflow-title">
            <header>
              <div>
                <span class="eyebrow">WORKFLOW</span>
                <h3 id="workflow-title">업무 진행 단계</h3>
              </div>
              <strong id="step-summary">1단계 · 요청 확인</strong>
            </header>
            <div class="workflow__steps" role="group" aria-label="업무 단계 선택">
              ${["요청 확인", "조건 검토", "실행 준비", "완료 확인"].map((step, index) => `
                <button type="button" class="workflow-step ${index === 0 ? "is-active" : ""}" data-step="${index}" aria-pressed="${index === 0}">
                  <span>${String(index + 1).padStart(2, "0")}</span>
                  ${step}
                </button>`).join("")}
            </div>
          </section>

          <section class="sample-section" aria-labelledby="sample-title">
            <header>
              <div>
                <span class="eyebrow">SAMPLE DATA</span>
                <h3 id="sample-title">대표 항목</h3>
              </div>
              <button class="text-button" type="button" data-refresh-sample>샘플 새로고침</button>
            </header>
            <div class="sample-list">
              ${samples.map((row, index) => `
                <div class="sample-row">
                  <span class="sample-row__index">${String(index + 1).padStart(2, "0")}</span>
                  <div>
                    <small>${row[0]}</small>
                    <strong>${row[1]}</strong>
                  </div>
                  <span class="sample-row__status">${row[2]}</span>
                </div>`).join("")}
            </div>
          </section>

          <section class="screen-note">
            <div>
              <span class="eyebrow">PREVIEW MODE</span>
              <h3>안전한 공개 체험</h3>
            </div>
            <p>이 페이지의 동작은 현재 브라우저에서만 보이며 서버 데이터, 주문, 운송, 재고와 결제를 변경하지 않습니다.</p>
          </section>
        </article>
      </div>
    </section>`;

  bindScreenInteractions(role);
}

function renderUnknown(path) {
  document.title = "준비 중인 화면 | Hongdal 체험";
  app.innerHTML = `
    <section class="unknown-page">
      <span class="eyebrow">PREVIEW ROUTE</span>
      <h1>이 경로는 아직 체험 목록에 없습니다.</h1>
      <p><code>${path}</code> 대신 역할별 대표 화면에서 현재 공개된 흐름을 확인할 수 있습니다.</p>
      <div class="unknown-page__actions">
        <a class="primary-button" href="/" data-route>체험 홈으로</a>
        <a class="secondary-button" href="https://github.com/cheolwo/hongdal" target="_blank" rel="noreferrer">GitHub 보기</a>
      </div>
    </section>`;
}

function renderSalesPageComposer() {
  document.title = "판매 페이지 만들기 | Hongdal 체험";
  app.innerHTML = `
    <section class="sales-composer-page">
      <nav class="breadcrumbs" aria-label="현재 위치">
        <a href="/" data-route>체험 홈</a>
        <span aria-hidden="true">/</span>
        <button type="button" data-select-role="shipper">화주·판매자</button>
        <span aria-hidden="true">/</span>
        <strong>판매 페이지 만들기</strong>
      </nav>

      <header class="sales-composer-hero">
        <div>
          <span class="eyebrow">HONGDAL SELLER PAGE</span>
          <h1>내 상품을 소개하고<br>주문 방법을 열어보세요</h1>
          <p>농가, 일반 판매자, 제조자, 수출업자 모두 사용할 수 있습니다. 공동주문은 판매 페이지의 종류가 아니라 구매자가 선택할 수 있는 주문 방식입니다.</p>
        </div>
        <span class="draft-pill">초안 · 실제 판매 미연결</span>
      </header>

      <div class="sales-composer-layout">
        <section class="seller-editor" aria-label="판매 페이지 입력">
          <div class="editor-section">
            <div class="section-heading"><span>01</span><div><h2>판매자와 상품</h2><p>누가 어떤 상품을 판매하는지 알려주세요.</p></div></div>
            <div class="seller-type-picker" role="group" aria-label="판매자 유형">
              <button type="button" class="is-selected" data-seller-type="일반 판매자">일반 판매자</button>
              <button type="button" data-seller-type="농가·생산자">농가·생산자</button>
              <button type="button" data-seller-type="수출업자">수출업자</button>
              <button type="button" data-seller-type="제조자">제조자</button>
            </div>
            <label class="field-label">판매자 표시명<input id="seller-name" value="햇살마켓" placeholder="예: 햇살농원"></label>
            <label class="field-label">상품명<input id="product-name" value="매콤한 크림 볶음면 5개 묶음" placeholder="상품 이름"></label>
            <label class="field-label">한 줄 소개<textarea id="product-tagline" rows="2">부드러운 크림과 매콤함을 함께 즐기는 간편한 한 끼</textarea></label>
          </div>

          <div class="editor-section editor-section--source">
            <div class="section-heading"><span>02</span><div><h2>외부 상세는 선택 사항</h2><p>직접 작성해도 되고, Amazon 상세 자료를 참고해 시작할 수도 있습니다.</p></div></div>
            <label class="field-label">Amazon 상품 상세 URL
              <div class="url-field"><input id="amazon-url" type="url" value="https://www.amazon.com/dp/B0CLWNBWVT"><button type="button" data-import-amazon>참고자료 불러오기</button></div>
            </label>
            <div class="source-result" id="source-result" aria-live="polite">
              <span class="source-result__icon" aria-hidden="true">A</span>
              <div><strong>외부 자료 호출 전</strong><small>불러온 가격과 재고는 Hongdal 판매 조건으로 자동 확정되지 않습니다.</small></div>
              <span class="source-result__status">선택</span>
            </div>
          </div>

          <div class="editor-section">
            <div class="section-heading"><span>03</span><div><h2>주문 방식</h2><p>판매자가 받을 수 있는 주문 방식을 선택합니다.</p></div></div>
            <div class="order-choice-grid">
              <label class="order-choice"><input type="checkbox" data-order-mode="individual" checked><span><strong>개별주문</strong><small>한 사람도 바로 주문할 수 있어요</small></span></label>
              <label class="order-choice"><input type="checkbox" data-order-mode="group" checked><span><strong>공동주문</strong><small>주문자 집단이 수량을 모아 제안해요</small></span></label>
            </div>
            <div class="price-row">
              <label class="field-label">Hongdal 판매가<input id="sales-price" inputmode="numeric" value="24,900"></label>
              <label class="field-label">공동주문 최소 수량<input id="group-minimum" inputmode="numeric" value="10"></label>
            </div>
          </div>

          <button class="create-draft-button" type="button" data-create-sales-draft>판매 페이지 초안 만들기 <span aria-hidden="true">→</span></button>
          <p class="safe-copy">초안 생성만 체험하며 실제 상품, 재고, 주문과 결제는 만들지 않습니다.</p>
        </section>

        <aside class="phone-preview" aria-label="모바일 판매 페이지 미리보기">
          <div class="phone-preview__bar"><span>모바일 미리보기</span><strong id="preview-state">작성 중</strong></div>
          <article class="seller-product-page">
            <div class="seller-product-page__media">
              <div class="product-photo-placeholder"><span aria-hidden="true">＋</span><strong>대표 상품 사진</strong><small>판매자가 직접 등록</small></div>
              <span class="seller-kind" id="preview-seller-type">일반 판매자</span>
            </div>
            <div class="seller-product-page__content">
              <div class="seller-profile"><span id="seller-initial">햇</span><div><small>판매자</small><strong id="preview-seller-name">햇살마켓</strong></div><button type="button" aria-label="판매자 정보 보기">정보</button></div>
              <div><h2 id="preview-product-name">매콤한 크림 볶음면 5개 묶음</h2><p id="preview-tagline">부드러운 크림과 매콤함을 함께 즐기는 간편한 한 끼</p></div>
              <strong class="preview-price" id="preview-price">24,900원</strong>
              <div class="purchase-facts"><span><small>주문 방식</small><strong id="preview-order-mode">개별 · 공동</strong></span><span><small>최소 주문</small><strong>1개</strong></span></div>
              <div class="group-purchase-box" id="group-order-box"><span aria-hidden="true">함께</span><div><strong>주문자 집단도 제안할 수 있어요</strong><small><b id="preview-group-minimum">10개</b>부터 수량을 모아 판매자에게 주문을 제안합니다.</small></div></div>
              <div class="source-snapshot" id="preview-source"><strong>외부 상세 참고 전</strong><small>외부 가격·재고는 별도 참고 영역에 표시됩니다.</small></div>
              <button class="preview-order-button" type="button" disabled>주문 방법 선택</button>
              <small class="preview-safety">판매상품과 재고가 연결된 뒤 공개할 수 있습니다.</small>
            </div>
          </article>
        </aside>
      </div>
    </section>`;

  bindSalesPageComposerInteractions();
}

function bindSalesPageComposerInteractions() {
  document.querySelector("[data-select-role]")?.addEventListener("click", () => {
    selectedRoleKey = "shipper";
    navigate("/");
  });

  document.querySelectorAll("[data-seller-type]").forEach((button) => {
    button.addEventListener("click", () => {
      document.querySelectorAll("[data-seller-type]").forEach((item) => item.classList.toggle("is-selected", item === button));
      document.querySelector("#preview-seller-type").textContent = button.dataset.sellerType;
    });
  });

  const mirrorText = (inputSelector, targetSelector, fallback) => {
    document.querySelector(inputSelector)?.addEventListener("input", (event) => {
      const value = event.currentTarget.value.trim() || fallback;
      document.querySelector(targetSelector).textContent = value;
      if (inputSelector === "#seller-name") document.querySelector("#seller-initial").textContent = value.slice(0, 1);
    });
  };
  mirrorText("#seller-name", "#preview-seller-name", "판매자 이름");
  mirrorText("#product-name", "#preview-product-name", "판매할 상품 이름");
  mirrorText("#product-tagline", "#preview-tagline", "상품의 특징과 판매자 이야기를 적어주세요.");

  document.querySelector("#sales-price")?.addEventListener("input", (event) => {
    document.querySelector("#preview-price").textContent = `${event.currentTarget.value.trim() || "가격 협의"}${event.currentTarget.value.trim() ? "원" : ""}`;
  });
  document.querySelector("#group-minimum")?.addEventListener("input", (event) => {
    document.querySelector("#preview-group-minimum").textContent = `${event.currentTarget.value.trim() || "2"}개`;
  });

  const updateOrderModes = () => {
    const individual = document.querySelector('[data-order-mode="individual"]').checked;
    const group = document.querySelector('[data-order-mode="group"]').checked;
    document.querySelector("#preview-order-mode").textContent = individual && group ? "개별 · 공동" : individual ? "개별주문" : group ? "공동주문" : "선택 필요";
    document.querySelector("#group-order-box").hidden = !group;
  };
  document.querySelectorAll("[data-order-mode]").forEach((input) => input.addEventListener("change", updateOrderModes));

  document.querySelector("[data-import-amazon]")?.addEventListener("click", (event) => {
    event.currentTarget.disabled = true;
    event.currentTarget.textContent = "자료 확인 완료";
    document.querySelector("#source-result").innerHTML = `
      <span class="source-result__icon" aria-hidden="true">A</span>
      <div><strong>Amazon 상세 1건 참고됨</strong><small>ASIN B0CLWNBWVT · 평점 4.3 (22) · 관측 가격 없음 · 현재 구매 불가</small></div>
      <span class="source-result__status is-complete">분리 저장</span>`;
    document.querySelector("#preview-source").innerHTML = `<strong>외부 상세 참고 · Amazon</strong><span>ASIN B0CLWNBWVT · 평점 4.3</span><small>관측 가격 없음 · 재고 없음. Hongdal 판매가와 재고로 자동 적용하지 않았습니다.</small>`;
    document.querySelector("#product-name").value = "Samyang Buldak Ramen Carbonara Bundle";
    document.querySelector("#preview-product-name").textContent = "Samyang Buldak Ramen Carbonara Bundle";
    showToast("외부 상품 상세를 참고자료로만 불러왔습니다.");
  });

  document.querySelector("[data-create-sales-draft]")?.addEventListener("click", () => {
    document.querySelector("#preview-state").textContent = "초안 저장됨";
    dialogCopy.textContent = "판매 페이지 초안을 만들었습니다. 실제 주문을 받으려면 판매자 소유의 입고상품 기반 판매상품을 연결하고 공개 검수를 거쳐야 합니다.";
    if (typeof dialog.showModal === "function") dialog.showModal();
    else dialog.setAttribute("open", "");
  });
}

function bindHomeInteractions() {
  document.querySelectorAll("[data-role-key]").forEach((button) => {
    button.addEventListener("click", () => {
      selectedRoleKey = button.dataset.roleKey;
      renderHome();
      document.querySelector(`[data-role-key="${selectedRoleKey}"]`)?.focus();
    });
  });
}

function bindScreenInteractions(role) {
  document.querySelector("[data-select-role]")?.addEventListener("click", () => {
    selectedRoleKey = role.key;
    navigate("/");
  });

  document.querySelectorAll("[data-step]").forEach((button) => {
    button.addEventListener("click", () => {
      document.querySelectorAll("[data-step]").forEach((item) => {
        const isActive = item === button;
        item.classList.toggle("is-active", isActive);
        item.setAttribute("aria-pressed", String(isActive));
      });
      const stepNames = ["요청 확인", "조건 검토", "실행 준비", "완료 확인"];
      const index = Number(button.dataset.step);
      document.querySelector("#step-summary").textContent = `${index + 1}단계 · ${stepNames[index]}`;
      showToast(`${stepNames[index]} 단계를 미리 보고 있습니다.`);
    });
  });

  document.querySelector("[data-demo-action]")?.addEventListener("click", (event) => {
    dialogCopy.textContent = `${event.currentTarget.dataset.demoAction} 동작을 샘플 상태로 확인했습니다. 실제 데이터는 변경되지 않습니다.`;
    if (typeof dialog.showModal === "function") dialog.showModal();
    else dialog.setAttribute("open", "");
  });

  document.querySelector("[data-refresh-sample]")?.addEventListener("click", (event) => {
    event.currentTarget.textContent = "샘플 확인 완료";
    showToast("최신 샘플 상태를 확인했습니다.");
  });
}

function render() {
  const path = normalizePath(window.location.pathname);
  if (path === "/") renderHome();
  else if (path === "/community") window.communitySalesPreview.renderBoard();
  else if (path === "/community/write") window.communitySalesPreview.renderComposer();
  else if (path === "/community/posts/101") window.communitySalesPreview.renderPost();
  else if (path === "/shipper/sales/pages/new") renderSalesPageComposer();
  else if (screenByPath.has(path)) renderScreen(screenByPath.get(path));
  else renderUnknown(path);
  bindRouteLinks();
}

function normalizePath(path) {
  if (!path || path === "/") return "/";
  return path.replace(/\/+$/, "") || "/";
}

function navigate(path) {
  const target = normalizePath(path);
  if (normalizePath(window.location.pathname) !== target) {
    window.history.pushState({}, "", target);
  }
  render();
  window.scrollTo({ top: 0, behavior: "instant" });
  app.focus({ preventScroll: true });
}

function bindRouteLinks() {
  document.querySelectorAll("a[data-route]").forEach((link) => {
    link.addEventListener("click", (event) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      event.preventDefault();
      navigate(new URL(link.href, window.location.origin).pathname);
    });
  });
}

function showToast(message) {
  window.clearTimeout(toastTimer);
  toast.textContent = message;
  toast.classList.add("is-visible");
  toastTimer = window.setTimeout(() => toast.classList.remove("is-visible"), 2600);
}

document.querySelector("[data-close-dialog]")?.addEventListener("click", () => dialog.close());
window.addEventListener("popstate", render);
render();
