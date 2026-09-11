class ProductHomePage {
    constructor() {
        this.productContainer = $('#product-container');
        this.productsPerRow = 4;
        this.currentPage = 1;
        this.pageSize = 8;
        this.totalCount = 0;
        this.pageSizeOptions = [8, 16, 24, 32];
        this.searchText = '';
        this.categoryId = null;
        this.sortBy = '';
        this.cartItems = [];
        this.aiSearchMode = false; // Phase 5 — semantic search toggle
    }

    async initialize() {
        try {
            const request = {
                page: this.currentPage,
                pageSize: this.pageSize,
                searchText: this.searchText,
                categoryId: this.categoryId,
                sortBy: this.sortBy,
                minPrice: null,
                maxPrice: null
            };
            const result = await ProductService.getProductList(request);
            this.totalCount = result.totalCount;
            this.cartItems = Common.getCartItems();
            this.changeCountQtyValue(this.cartItems, result.products);
            this.renderProducts(result.products, result.page, result.totalCount);
        } catch (error) {
            console.error('Error initializing product page:', error);
            this.showError('Failed to load products. Please try again later.');
        }
    }

    async changeCountQtyValue(cartItems, products) {
        for (let i = 0; i < products.length; i++) {
            for (let j = 0; j < cartItems.length; j++) {
                if (products[i].id == cartItems[j].productId) {
                    products[i].selectedQty = cartItems[j].quantity;
                    products[i].cartId = cartItems[j].id;
                }
            }
        }
    }

    async changePage(newPage) {
        if (newPage < 1 || newPage > this.getTotalPages()) return;
        
        this.currentPage = newPage;
        await this.fetchAndRenderProducts();
    }

    async changePageSize(newSize) {
        this.pageSize = newSize;
        this.currentPage = 1; // Reset to first page when changing page size
        await this.fetchAndRenderProducts();
    }

    async fetchAndRenderProducts() {
        this.showLoading();
        try {
            const request = {
                page: this.currentPage,
                pageSize: this.pageSize,
                searchText: this.searchText,
                categoryId: this.categoryId,
                sortBy: this.sortBy,
                minPrice: null,
                maxPrice: null
            };
            const result = await ProductService.getProductList(request);
            this.totalCount = result.totalCount;
            this.changeCountQtyValue(this.cartItems, result.products);
            this.renderProducts(result.products, result.page, result.totalCount);
        } catch (error) {
            console.error('Error fetching products:', error);
            this.showError('Failed to load products. Please try again later.');
        }
    }

    async handleSearch() {
        this.searchText = $('.search-input').val().trim();
        this.currentPage = 1; // Reset to first page when searching
        if (this.aiSearchMode) {
            await this.runAiSearch();
        } else {
            await this.fetchAndRenderProducts();
        }
    }

    // ── Phase 5: semantic search ─────────────────────────────────────────────
    async runAiSearch() {
        const query = this.searchText;
        if (!query) {
            Toast.warning('Type what you\'re looking for first.');
            return;
        }
        this.showLoading();
        try {
            const result = await AiService.searchProducts(query, 20);
            this.renderAiSearchResults(result.results, result.query);
        } catch (error) {
            console.error('AI search failed:', error);
            Toast.error((error.message || 'AI search is unavailable right now') + ' — showing keyword search instead.');
            this.aiSearchMode = false;
            $('#ai-search-toggle').removeClass('btn-primary active').addClass('btn-outline-primary');
            $('#categoryFilter, #sortFilter').prop('disabled', false);
            await this.fetchAndRenderProducts();
        }
    }

    renderAiSearchResults(results, query) {
        if (!this.productContainer.length) return;
        this.productContainer.empty();

        const safeQuery = $('<div>').text(query).html();
        this.productContainer.append(`
            <div class="px-5 py-2 d-flex justify-content-between align-items-center flex-wrap gap-2">
                <p class="results-count mb-0">
                    <i class="fas fa-wand-magic-sparkles me-1" style="color:var(--accent)"></i>
                    AI matches for &ldquo;<strong>${safeQuery}</strong>&rdquo; (${results.length})
                </p>
                <button type="button" class="btn btn-sm btn-link p-0" id="ai-search-clear">Clear AI search</button>
            </div>`);

        if (!results.length) {
            this.productContainer.append(`
                <div class="px-5 py-4 text-muted">
                    No close matches for that. Try rephrasing, or turn off AI Search for a keyword search.
                </div>`);
            return;
        }

        for (let i = 0; i < results.length; i += this.productsPerRow) {
            const $row = $('<div class="px-5">').addClass('row');
            results.slice(i, i + this.productsPerRow).forEach(product => {
                const itemCard = new ItemCard(product);
                const $card = itemCard.createCard();
                $card.find('.product-image').closest('.position-relative').append(`
                    <span class="badge position-absolute bottom-0 start-0 m-2"
                          style="background:var(--accent);color:#fff;font-weight:500">
                        <i class="fas fa-wand-magic-sparkles me-1"></i>${Math.round(product.score * 100)}% match
                    </span>`);
                $row.append($card);
            });
            this.productContainer.append($row);
        }
    }

    getTotalPages() {
        return Math.ceil(this.totalCount / this.pageSize);
    }

    renderProducts(products, pageNumber, totalCount) {
        if (!this.productContainer.length) {
            console.error('Product container not found');
            return;
        }

        // Clear existing content
        this.productContainer.empty();

        var startItemNo = ((pageNumber - 1) * this.pageSize) + 1;
        var endItemNo = startItemNo + products.length - 1;
        if (totalCount < endItemNo) {
            endItemNo = totalCount;
        }

        var headInfo = '<div class="px-5 py-2"><p class="results-count mb-0">Showing <span>' + startItemNo + '–' + endItemNo + '</span> of <span>' + totalCount + '</span> results</p></div>';

        this.productContainer.append(headInfo);

        // Create rows of products
        for (let i = 0; i < products.length; i += this.productsPerRow) {
            const $row = $('<div class="px-5">').addClass('row');

            // Add products to the row
            const rowProducts = products.slice(i, i + this.productsPerRow);
            rowProducts.forEach(product => {
                const itemCard = new ItemCard(product);
                $row.append(itemCard.createCard());
            });

            this.productContainer.append($row);
        }

        // Add pagination controls
        this.renderPagination();
    }

    renderPagination() {
        const totalPages = this.getTotalPages();
        //if (totalPages <= 1) return;

        const $paginationContainer = $('<div class="d-flex justify-content-end align-items-center px-5 py-3">');
        
        // Page size selector
        const $pageSizeContainer = $('<div class="d-flex align-items-center gap-2 px-2">');
        const $pageSizeSelect = $('<select class="form-select form-select-sm" style="width: 120px; text-align: center">');
        
        this.pageSizeOptions.forEach(size => {
            const $option = $('<option>')
                .val(size)
                .text(size + " per page")
                .prop('selected', size === this.pageSize);
            $pageSizeSelect.append($option);
        });

        $pageSizeSelect.on('change', (e) => this.changePageSize(parseInt(e.target.value)));
        $pageSizeContainer.append($pageSizeSelect);

        // Pagination controls
        const $pagination = $('<nav>').attr('aria-label', 'Product pagination');
        const $paginationList = $('<ul class="pagination mb-0">');

        // Previous button
        const $prevLi = $('<li class="page-item">').addClass(this.currentPage === 1 ? 'disabled' : '');
        const $prevButton = $('<button class="page-link">')
            .html('<i class="fas fa-arrow-left me-1"></i> Prev')
            .on('click', () => this.changePage(this.currentPage - 1));
        $prevLi.append($prevButton);
        $paginationList.append($prevLi);

        // Page numbers
        for (let i = 1; i <= totalPages; i++) {
            const $pageLi = $('<li class="page-item">').addClass(i === this.currentPage ? 'active' : '');
            const $pageButton = $('<button class="page-link">')
                .text(i)
                .on('click', () => this.changePage(i));
            $pageLi.append($pageButton);
            $paginationList.append($pageLi);
        }

        // Next button
        const $nextLi = $('<li class="page-item">').addClass(this.currentPage === totalPages ? 'disabled' : '');
        const $nextButton = $('<button class="page-link">')
            .html('Next <i class="fas fa-arrow-right ms-1"></i>')
            .on('click', () => this.changePage(this.currentPage + 1));
        $nextLi.append($nextButton);
        $paginationList.append($nextLi);

        $pagination.append($paginationList);
        $paginationContainer.append($pagination, $pageSizeContainer);
        this.productContainer.append($paginationContainer);
    }

    renderTopCards() {
        const cards = [
            {
                logo: '<i class="fas fa-university fa-2x"></i>',
                title: 'Total Products: 100',
                subtitle: 'Warehouse has total of 100 product today & max capacity is 200.'
            },
            {
                logo: '<i class="fas fa-user-secret fa-2x"></i>',
                title: 'Total Vendor: 6',
                subtitle: 'A total of out of 6 out of 10 vendor are available for supply now.'
            },
            {
                logo: '<i class="fas fa-cube fa-2x"></i>',
                title: 'Unique Product: 40',
                subtitle: 'Total number of products thats are not duplicate or redundant.'
            }
        ];

        const $topCardsContainer = $('<div class="row px-3 py-4">');
        
        cards.forEach(card => {
            const $card = $(`
                <div class="col-md-4">
                    <div class="top-card">
                        <div class="top-card-content">
                            <div class="top-card-logo">
                                ${card.logo}
                            </div>
                            <h3 class="top-card-title">${card.title}</h3>
                            <p class="top-card-subtitle">${card.subtitle}</p>
                        </div>
                    </div>
                </div>
            `);
            $topCardsContainer.append($card);
        });

        $("#topCardsContainer").append($topCardsContainer);
    }

    showLoading() {
        const skeletonCard = `
            <div class="col-md-3 mb-4" style="padding:8px">
                <div class="card p-0 placeholder-glow" style="border-radius:var(--radius-lg);overflow:hidden">
                    <div class="placeholder col-12" style="height:180px;border-radius:0"></div>
                    <div style="padding:0.85rem 1rem">
                        <div class="placeholder col-8 mb-2" style="height:1rem"></div>
                        <div class="placeholder col-5 mb-3" style="height:0.8rem"></div>
                        <div class="placeholder col-4" style="height:1.2rem"></div>
                    </div>
                </div>
            </div>`;
        this.productContainer.html(`<div class="px-5"><div class="row">${skeletonCard.repeat(8)}</div></div>`);
    }

    showError(message) {
        if (this.productContainer.length) {
            this.productContainer.html(`
                <div class="alert alert-danger" role="alert">
                    ${message}
                </div>
            `);
        }
    }
}

// Initialize the page when the DOM is loaded
$(document).ready(async () => {
    window.productPage = new ProductHomePage();
    productPage.initialize();

    // Role-based admin UI
    const _isAdmin = Common.getUserInfo().role === 'Admin';
    if (_isAdmin) {
        $('#admin-add-product-col').removeClass('d-none');
    }

    // Load categories into the filter dropdown
    try {
        const categories = await CategoryService.getCategories();
        const $filter = $('#categoryFilter');
        categories.forEach(cat => {
            $filter.append(`<option value="${cat.id}">${cat.name}</option>`);
        });
        $filter.on('change', () => {
            productPage.categoryId = $filter.val() ? parseInt($filter.val()) : null;
            productPage.currentPage = 1;
            productPage.fetchAndRenderProducts();
        });
    } catch (e) {
        console.warn('Could not load categories:', e);
    }

    // Sort
    $('#sortFilter').on('change', function () {
        productPage.sortBy = $(this).val() || '';
        productPage.currentPage = 1;
        productPage.fetchAndRenderProducts();
    });

    // Search autocomplete
    let _acTimer = null;
    const $acInput = $('.search-input');
    const $acDrop = $('<div>').css({
        position: 'absolute', top: '100%', left: 0, right: 0,
        background: 'var(--bs-body-bg, #fff)', border: '1px solid #ddd',
        borderRadius: '0 0 8px 8px', boxShadow: '0 4px 12px rgba(0,0,0,.12)',
        zIndex: 1050, maxHeight: '280px', overflowY: 'auto', display: 'none'
    });
    $('.search-container').css('position', 'relative').append($acDrop);

    $acInput.on('input', function () {
        clearTimeout(_acTimer);
        const val = $(this).val().trim();
        if (val.length < 2) { $acDrop.hide(); return; }
        _acTimer = setTimeout(async () => {
            try {
                const r = await ProductService.getProductList({ searchText: val, page: 1, pageSize: 6 });
                if (!r.products.length) { $acDrop.hide(); return; }
                $acDrop.html(r.products.map(p => `
                    <div class="ac-item d-flex justify-content-between align-items-center px-3 py-2"
                         style="cursor:pointer;border-bottom:1px solid rgba(0,0,0,.05)"
                         data-id="${p.id}" data-name="${p.name.replace(/"/g, '&quot;')}">
                        <span class="fw-semibold small">${p.name}</span>
                        <span class="text-muted small ms-2">৳${p.price.toFixed(2)}</span>
                    </div>`).join('')).show();
            } catch { $acDrop.hide(); }
        }, 300);
    });

    $acDrop.on('mouseenter', '.ac-item', function () { $(this).addClass('bg-light'); })
           .on('mouseleave', '.ac-item', function () { $(this).removeClass('bg-light'); })
           .on('click', '.ac-item', function () {
               $acInput.val($(this).data('name'));
               $acDrop.hide();
               productPage.handleSearch();
           });

    $(document).on('click', e => {
        if (!$(e.target).closest('.search-container').length) $acDrop.hide();
    });

    $acInput.on('keydown', e => { if (e.key === 'Escape') $acDrop.hide(); });

    // Search
    $('.search-input').on('keypress', function (e) {
        if (e.which === 13) { e.preventDefault(); productPage.handleSearch(); }
    });
    $('.search-icon').on('click', () => productPage.handleSearch());

    // ── AI (semantic) search toggle ──────────────────────────────────────────
    $('#ai-search-toggle').on('click', function () {
        productPage.aiSearchMode = !productPage.aiSearchMode;
        $(this).toggleClass('btn-primary active', productPage.aiSearchMode)
               .toggleClass('btn-outline-primary', !productPage.aiSearchMode);
        $('#categoryFilter, #sortFilter').prop('disabled', productPage.aiSearchMode);
        if ($('.search-input').val().trim()) productPage.handleSearch();
    });
    // "Clear AI search" — rendered dynamically inside AI results, so delegate.
    $(document).on('click', '#ai-search-clear', () => {
        productPage.aiSearchMode = false;
        $('#ai-search-toggle').removeClass('btn-primary active').addClass('btn-outline-primary');
        $('#categoryFilter, #sortFilter').prop('disabled', false);
        $('.search-input').val('');
        productPage.searchText = '';
        productPage.currentPage = 1;
        productPage.fetchAndRenderProducts();
    });

    // ── Admin product management ─────────────────────────────────────────────
    // Open Add Product modal
    $(document).on('click', '.admin-add-product-btn', () => ProductAdminModal.openAdd());

    // Edit product — delegated (card buttons)
    $(document).on('click', '.admin-edit-btn', function () {
        const product = JSON.parse($(this).attr('data-product'));
        ProductAdminModal.openEdit(product);
    });

    // Delete product — delegated (card buttons)
    $(document).on('click', '.admin-delete-btn', async function () {
        const $btn = $(this);
        const id = parseInt($btn.data('productId'));
        const name = $btn.data('productName');
        if (!confirm(`Delete "${name}"?\n\nThis cannot be undone.`)) return;
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');
        try {
            await ProductService.deleteProduct(id);
            Toast.success(`"${name}" deleted.`);
            await productPage.fetchAndRenderProducts();
        } catch (e) {
            Toast.error(e.message || 'Failed to delete product.');
            $btn.prop('disabled', false).html('<i class="fas fa-trash"></i> Delete');
        }
    });

    // ── Wishlist toggle ──────────────────────────────────────────────────────
    $(document).on('click', '.wishlist-toggle-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const product = JSON.parse($(this).attr('data-product-json'));
        const added = WishlistService.toggle(product);
        $(this).css('color', added ? 'var(--danger)' : 'var(--neutral-400)');
        Toast.info(added ? `"${product.name}" added to wishlist` : `Removed from wishlist`);
    });
}); 