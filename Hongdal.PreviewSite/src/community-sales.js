(() => {
  const salesPost = {
    boardTitle: "오늘 수확한 복숭아, 필요한 분 계실까요?",
    body: "아침에 수확한 복숭아입니다. 크기가 조금 달라도 맛은 좋아요. 수령 시간과 묶음 주문은 댓글로 편하게 이야기해 주세요.",
    seller: "햇살농원",
    productTitle: "햇복숭아 3kg 한 상자",
    quantity: "24",
    unit: "상자",
    price: "29,000",
    methods: ["토스결제", "네이버페이", "PayPal", "당사자 간 현금"],
    allowsGroup: true,
    thumbnailName: "복숭아-대표.jpg",
    detailName: "복숭아-상세.jpg"
  };

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  function renderBoard() {
    document.title = "생활 게시판 | Hongdal 체험";
    app.innerHTML = [
      '<section class="sales-board">',
      '  <header class="sales-board__appbar">',
      '    <a class="sales-board__back" href="/" data-route aria-label="체험 홈으로">‹</a>',
      '    <div><strong>생활 게시판</strong><small>판매 · 나눔 · 동네 이야기</small></div>',
      '    <button type="button" aria-label="게시판 검색">⌕</button>',
      '  </header>',
      '  <nav class="sales-board__tabs" aria-label="게시판 분류"><button class="is-active" data-board-filter="all">전체</button><button data-board-filter="생활">생활</button><button data-board-filter="판매">판매</button><button data-board-filter="공동구매">공동구매</button></nav>',
      '  <div class="sales-board__notice"><span>안내</span><p>상품 정보는 대화를 돕는 요약입니다. 주문·결제 조건은 당사자가 확인해 주세요.</p></div>',
      '  <div class="sales-board__feed">',
      '    <a class="sales-board-card sales-board-card--product" href="/community/posts/101" data-board-category="판매" data-route>',
      '      <div class="sales-board-card__thumb"><span>🍑</span><small>썸네일</small></div>',
      '      <div class="sales-board-card__copy">',
      '        <div class="sales-board-card__meta"><span>판매 중</span><small>방금 · ' + escapeHtml(salesPost.seller) + '</small></div>',
      '        <h2>' + escapeHtml(salesPost.boardTitle) + '</h2>',
      '        <strong>' + escapeHtml(salesPost.productTitle) + '</strong>',
      '        <div class="sales-board-card__facts"><b>' + escapeHtml(salesPost.price) + '원</b><span>남은 수량 ' + escapeHtml(salesPost.quantity) + escapeHtml(salesPost.unit) + '</span></div>',
      '        <p>결제 협의 ' + salesPost.methods.length + '가지 · 댓글로 구매 문의</p>',
      '      </div>',
      '    </a>',
      '    <article class="sales-board-card sales-board-card--text" data-board-category="생활"><div><span>생활 질문</span><small>12분 · 동네사람</small></div><h2>이번 주말 나눔장터 장소가 정해졌나요?</h2><p>댓글 5 · 추천 3</p></article>',
      '    <article class="sales-board-card sales-board-card--text" data-board-category="공동구매"><div><span>공동구매</span><small>1시간 · 장바구니</small></div><h2>친환경 세제 12개 묶음 같이 주문하실 분</h2><p>댓글 8 · 관심 11</p></article>',
      '  </div>',
      '  <a class="sales-board__write" href="/community/write" data-route><span>＋</span> 글쓰기</a>',
      '  <nav class="sales-mobile-nav" aria-label="모바일 메뉴"><a class="is-active" href="/community" data-route><span>⌂</span>게시판</a><button><span>◎</span>원장</button><button><span>♙</span>내 활동</button></nav>',
      '</section>'
    ].join("");

    document.querySelectorAll("[data-board-filter]").forEach((button) => {
      button.addEventListener("click", () => {
        const selected = button.dataset.boardFilter;
        document.querySelectorAll("[data-board-filter]").forEach((item) => item.classList.toggle("is-active", item === button));
        document.querySelectorAll("[data-board-category]").forEach((card) => {
          card.hidden = selected !== "all" && card.dataset.boardCategory !== selected;
        });
      });
    });
  }

  function renderComposer() {
    document.title = "판매글 작성 | Hongdal 체험";
    app.innerHTML = [
      '<section class="sales-write">',
      '  <header class="sales-write__appbar">',
      '    <a href="/community" data-route aria-label="게시판으로 돌아가기">‹</a>',
      '    <div><strong>글쓰기</strong><small>판매 정보를 붙인 커뮤니티 글</small></div>',
      '    <button type="submit" form="sales-post-form">등록</button>',
      '  </header>',
      '  <form id="sales-post-form" class="sales-write__form">',
      '    <label class="sales-write__plain">게시글 제목<input name="boardTitle" value="' + escapeHtml(salesPost.boardTitle) + '" maxlength="160" required></label>',
      '    <label class="sales-write__plain">내용<textarea name="body" rows="5">' + escapeHtml(salesPost.body) + '</textarea></label>',
      '    <section class="sales-write__product">',
      '      <div class="sales-write__destination"><span>게시 위치</span><strong>판매 게시판</strong><small>판매 정보가 있어 자동으로 분류됩니다.</small></div>',
      '      <header><div><span>판매 정보</span><strong>상품 요약을 게시글에 붙입니다</strong></div><span class="sales-write__on">사용 중</span></header>',
      '      <label>상품명<input name="productTitle" value="' + escapeHtml(salesPost.productTitle) + '" required></label>',
      '      <div class="sales-write__numbers">',
      '        <label>판매 가능 수량<input name="quantity" type="number" min="1" value="' + escapeHtml(salesPost.quantity) + '" required></label>',
      '        <label>단위<input name="unit" value="' + escapeHtml(salesPost.unit) + '" required></label>',
      '        <label>개당 가격<input name="price" inputmode="numeric" value="' + escapeHtml(salesPost.price) + '" required></label>',
      '      </div>',
      '      <div class="sales-write__photos">',
      '        <label><input type="file" accept="image/*" data-sales-photo="thumbnail"><span class="sales-write__photo-art">🍑</span><b>썸네일</b><small id="thumbnail-name">' + escapeHtml(salesPost.thumbnailName) + '</small></label>',
      '        <label><input type="file" accept="image/*" data-sales-photo="detail"><span class="sales-write__photo-art sales-write__photo-art--detail">＋</span><b>상세 사진</b><small id="detail-name">' + escapeHtml(salesPost.detailName) + '</small></label>',
      '      </div>',
      '      <fieldset><legend>협의 가능한 결제</legend>',
      '        <label><input type="checkbox" name="method" value="토스결제" checked><span>토스결제</span></label>',
      '        <label><input type="checkbox" name="method" value="네이버페이" checked><span>네이버페이</span></label>',
      '        <label><input type="checkbox" name="method" value="PayPal" checked><span>PayPal</span></label>',
      '        <label><input type="checkbox" name="method" value="당사자 간 현금" checked><span>당사자 간 현금</span></label>',
      '      </fieldset>',
      '      <label class="sales-write__group"><input type="checkbox" name="allowsGroup" checked><span><strong>공동구매 제안 허용</strong><small>주문자들이 수량을 모아 댓글로 제안할 수 있어요.</small></span></label>',
      '      <p class="sales-write__payment-note">결제 수단은 가능 여부만 알립니다. 실제 결제 연결과 현금 전달은 주문 조건을 합의한 뒤 별도로 확인합니다.</p>',
      '    </section>',
      '    <section class="sales-write__identity"><strong>작성자 설정</strong><label>판매자 표시명<input name="seller" value="' + escapeHtml(salesPost.seller) + '"></label><label>글 비밀번호<input type="password" value="1234"></label></section>',
      '    <button class="sales-write__submit" type="submit">판매글 등록하기</button>',
      '    <small class="sales-write__safe">체험 사이트에서는 브라우저 안의 샘플 글만 바뀌며 실제 주문이나 결제가 생성되지 않습니다.</small>',
      '  </form>',
      '</section>'
    ].join("");

    document.querySelectorAll("[data-sales-photo]").forEach((input) => {
      input.addEventListener("change", (event) => {
        const file = event.currentTarget.files?.[0];
        if (!file) return;
        const target = event.currentTarget.dataset.salesPhoto === "thumbnail" ? "#thumbnail-name" : "#detail-name";
        document.querySelector(target).textContent = file.name;
      });
    });

    document.querySelector("#sales-post-form")?.addEventListener("submit", (event) => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      const methods = data.getAll("method").map(String);
      if (methods.length === 0) {
        showToast("협의 가능한 결제 방법을 하나 이상 선택해 주세요.");
        return;
      }

      salesPost.boardTitle = String(data.get("boardTitle") || "").trim();
      salesPost.body = String(data.get("body") || "").trim();
      salesPost.productTitle = String(data.get("productTitle") || "").trim();
      salesPost.quantity = String(data.get("quantity") || "1").trim();
      salesPost.unit = String(data.get("unit") || "개").trim();
      salesPost.price = String(data.get("price") || "0").trim();
      salesPost.seller = String(data.get("seller") || "판매자").trim();
      salesPost.methods = methods;
      salesPost.allowsGroup = data.get("allowsGroup") === "on";
      salesPost.thumbnailName = document.querySelector("#thumbnail-name")?.textContent || salesPost.thumbnailName;
      salesPost.detailName = document.querySelector("#detail-name")?.textContent || salesPost.detailName;
      showToast("판매 게시판에 글을 등록했습니다.");
      navigate("/community/posts/101");
    });
  }

  function renderPost() {
    document.title = salesPost.boardTitle + " | Hongdal 체험";
    app.innerHTML = [
      '<section class="sales-post">',
      '  <header class="sales-post__appbar"><a href="/community" data-route aria-label="게시판으로">‹</a><strong>판매 게시글</strong><button type="button" aria-label="더 보기">···</button></header>',
      '  <article class="sales-post__article">',
      '    <header class="sales-post__author"><span>' + escapeHtml(salesPost.seller.slice(0, 1)) + '</span><div><strong>' + escapeHtml(salesPost.seller) + '</strong><small>생산자 · 방금</small></div><b>판매 중</b></header>',
      '    <h1>' + escapeHtml(salesPost.boardTitle) + '</h1>',
      '    <p class="sales-post__body">' + escapeHtml(salesPost.body) + '</p>',
      '    <section class="sales-post__offer">',
      '      <div class="sales-post__hero"><span>🍑</span><b>썸네일</b><small>' + escapeHtml(salesPost.thumbnailName) + '</small></div>',
      '      <div class="sales-post__offer-copy">',
      '        <div class="sales-post__badges"><span>판매 중</span>' + (salesPost.allowsGroup ? '<span>공동구매 제안 가능</span>' : '') + '</div>',
      '        <h2>' + escapeHtml(salesPost.productTitle) + '</h2>',
      '        <div class="sales-post__price"><strong>' + escapeHtml(salesPost.price) + '원</strong><span>/ ' + escapeHtml(salesPost.unit) + '</span><small>남은 수량 ' + escapeHtml(salesPost.quantity) + escapeHtml(salesPost.unit) + '</small></div>',
      '        <div class="sales-post__methods">' + salesPost.methods.map((method) => '<span>' + escapeHtml(method) + '</span>').join("") + '</div>',
      '        <div class="sales-post__inquiry">',
      '          <label>희망 수량<input id="inquiry-quantity" type="number" min="1" max="' + escapeHtml(salesPost.quantity) + '" value="1"></label>',
      '          <label>희망 결제<select id="inquiry-method">' + salesPost.methods.map((method) => '<option>' + escapeHtml(method) + '</option>').join("") + '</select></label>',
      '          <button type="button" data-build-inquiry>구매 문의 남기기</button>',
      '        </div>',
      '        <p class="sales-post__notice">댓글로 수량·수령·결제 조건을 합의하세요. 당사자 간 현금 거래는 플랫폼이 지급·수령을 보증하지 않습니다.</p>',
      '      </div>',
      '    </section>',
      '    <section class="sales-post__details"><header><strong>상세 사진</strong><span>2장</span></header><div><figure><span>🍑</span><figcaption>대표 상품 사진</figcaption></figure><figure><span>📦</span><figcaption>' + escapeHtml(salesPost.detailName) + '</figcaption></figure></div></section>',
      '    <div class="sales-post__metrics"><span>♡ 추천 4</span><span>댓글 2</span><button type="button">공유</button></div>',
      '    <section class="sales-post__comments"><header><strong>대화 2</strong><small>거래 조건은 공개 댓글에서 먼저 확인하세요.</small></header><article><span>민지</span><div><strong>구매 문의</strong><p>2상자 가능할까요? 토요일 오전 수령을 원해요.</p></div></article><article><span>햇</span><div><strong>' + escapeHtml(salesPost.seller) + '</strong><p>가능합니다. 다른 분들과 묶음 주문도 열어둘게요.</p></div></article><div id="new-sales-comment"></div></section>',
      '    <section class="sales-post__comment-form" id="sales-comment-form"><textarea id="inquiry-comment" rows="3" placeholder="상품이나 수령 방법을 댓글로 물어보세요."></textarea><button type="button" data-submit-inquiry>댓글 등록</button></section>',
      '  </article>',
      '</section>'
    ].join("");

    document.querySelector("[data-build-inquiry]")?.addEventListener("click", () => {
      const quantity = document.querySelector("#inquiry-quantity")?.value || "1";
      const method = document.querySelector("#inquiry-method")?.value || salesPost.methods[0];
      const text = "[구매 문의] " + salesPost.productTitle + " " + quantity + salesPost.unit + ", 희망 결제: " + method + ". 수령 방법과 최종 조건을 이야기하고 싶습니다.";
      document.querySelector("#inquiry-comment").value = text;
      document.querySelector("#sales-comment-form").scrollIntoView({ behavior: "smooth", block: "center" });
      document.querySelector("#inquiry-comment").focus();
    });

    document.querySelector("[data-submit-inquiry]")?.addEventListener("click", () => {
      const textarea = document.querySelector("#inquiry-comment");
      const body = textarea?.value.trim();
      if (!body) {
        showToast("댓글 내용을 입력해 주세요.");
        return;
      }
      document.querySelector("#new-sales-comment").innerHTML = '<article class="is-new"><span>나</span><div><strong>구매 문의 · 방금</strong><p>' + escapeHtml(body) + '</p></div></article>';
      textarea.value = "";
      showToast("구매 문의 댓글을 등록했습니다.");
    });
  }

  window.communitySalesPreview = { renderBoard, renderComposer, renderPost };
})();
