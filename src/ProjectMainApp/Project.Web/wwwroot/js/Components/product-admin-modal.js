/**
 * ProductAdminModal — shared Add / Edit product modal.
 * Requires: jQuery, Bootstrap 5, ProductService, CategoryService, Toast
 */
const ProductAdminModal = {
    _modal: null,
    _editingId: null,

    _build() {
        if ($('#pm-modal').length) return;

        $('body').append(`
            <div class="modal fade" id="pm-modal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content px-4 py-3">
                        <div class="modal-header border-0">
                            <div class="w-100">
                                <h5 class="modal-title fw-bold" id="pm-modal-title">Add Product</h5>
                                <p class="text-muted small mb-0" id="pm-modal-subtitle">Fill in the details below</p>
                            </div>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <form id="pm-form" novalidate>
                                <div class="mb-3">
                                    <label class="form-label fw-semibold">Product Name <span class="text-danger">*</span></label>
                                    <input type="text" id="pm-name" class="form-control" placeholder="e.g. Wireless Headphones" required>
                                </div>
                                <div class="mb-3">
                                    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-1">
                                        <label class="form-label fw-semibold mb-0">Description</label>
                                        <div class="d-none align-items-center gap-2" id="pm-ai-row">
                                            <select id="pm-ai-tone" class="form-select form-select-sm" style="width:auto" title="Tone">
                                                <option value="">Friendly &amp; professional</option>
                                                <option value="premium and polished">Premium</option>
                                                <option value="playful and fun">Playful</option>
                                                <option value="technical and precise">Technical</option>
                                            </select>
                                            <button type="button" class="btn btn-sm btn-outline-primary" id="pm-ai-generate">
                                                <i class="fas fa-wand-magic-sparkles me-1"></i>Generate with AI
                                            </button>
                                        </div>
                                    </div>
                                    <textarea id="pm-desc" class="form-control" rows="2" placeholder="Short description..."></textarea>
                                    <div class="form-text" id="pm-ai-hint">Save the product first to generate an AI description.</div>
                                </div>
                                <div class="row mb-3">
                                    <div class="col-md-6">
                                        <label class="form-label fw-semibold">Slug <span class="text-danger">*</span></label>
                                        <div class="input-group">
                                            <input type="text" id="pm-slug" class="form-control" placeholder="product-slug" required>
                                            <button class="btn btn-outline-secondary" type="button" id="pm-gen-slug">Generate</button>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label fw-semibold">Price <span class="text-danger">*</span></label>
                                        <input type="number" id="pm-price" class="form-control" step="0.01" min="0.01" placeholder="0.00" required>
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label fw-semibold">Stock</label>
                                        <input type="number" id="pm-stock" class="form-control" min="0" value="0">
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label fw-semibold">Category</label>
                                    <select id="pm-category" class="form-select">
                                        <option value="">— Select category —</option>
                                    </select>
                                </div>
                                <div class="row mb-3">
                                    <div class="col-md-6">
                                        <label class="form-label fw-semibold">Discount Start</label>
                                        <input type="date" id="pm-disc-start" class="form-control">
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label fw-semibold">Discount End</label>
                                        <input type="date" id="pm-disc-end" class="form-control">
                                    </div>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer border-0">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" id="pm-save" class="btn btn-primary px-4">
                                <i class="fas fa-save me-1"></i>Add Product
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `);

        this._modal = new bootstrap.Modal(document.getElementById('pm-modal'));

        // Slug auto-generate
        $('#pm-gen-slug').on('click', () => {
            const name = $('#pm-name').val().trim();
            if (name) $('#pm-slug').val(this._toSlug(name));
        });
        $('#pm-name').on('input', function () {
            if (!$('#pm-slug').val())
                $('#pm-slug').val(ProductAdminModal._toSlug($(this).val()));
        });

        $('#pm-save').on('click', () => this._save());
        $('#pm-ai-generate').on('click', () => this._generateDescription());
    },

    async _generateDescription() {
        if (!this._editingId) return; // AI generation needs a saved product to read facts from

        const $btn = $('#pm-ai-generate');
        const tone = $('#pm-ai-tone').val();
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Generating...');

        try {
            const result = await AiService.generateDescription(this._editingId, tone ? { tone } : {});
            $('#pm-desc').val(result.draft);
            Toast.success(result.cached
                ? 'Loaded a cached AI draft — edit as needed before saving.'
                : 'AI draft generated — review and edit before saving.');
        } catch (e) {
            Toast.error(e.message || 'Could not generate a description right now.');
        } finally {
            $btn.prop('disabled', false).html('<i class="fas fa-wand-magic-sparkles me-1"></i>Generate with AI');
        }
    },

    _toSlug(name) {
        return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
    },

    async _loadCategories(selectedId) {
        try {
            const cats = await CategoryService.getCategories();
            const $sel = $('#pm-category');
            $sel.find('option:not(:first)').remove();
            cats.forEach(c => $sel.append(`<option value="${c.id}">${c.name}</option>`));
            if (selectedId) $sel.val(selectedId);
        } catch (e) { /* ignore — category is optional */ }
    },

    openAdd() {
        this._build();
        this._editingId = null;
        $('#pm-modal-title').text('Add New Product');
        $('#pm-modal-subtitle').text('Fill in the details for the new product');
        $('#pm-form')[0].reset();
        $('#pm-stock').val(0);
        $('#pm-save').html('<i class="fas fa-plus me-1"></i>Add Product');
        $('#pm-ai-row').removeClass('d-flex').addClass('d-none');
        $('#pm-ai-hint').removeClass('d-none');
        this._loadCategories(null);
        this._modal.show();
    },

    openEdit(product) {
        this._build();
        this._editingId = product.id;
        $('#pm-modal-title').text('Edit Product');
        $('#pm-modal-subtitle').text('Update the product information below');
        $('#pm-name').val(product.name || '');
        $('#pm-desc').val(product.description || '');
        $('#pm-slug').val(product.slug || '');
        $('#pm-price').val(product.price ?? '');
        $('#pm-stock').val(product.stock ?? 0);
        $('#pm-disc-start').val(product.discountStartDate ? product.discountStartDate.substring(0, 10) : '');
        $('#pm-disc-end').val(product.discountEndDate ? product.discountEndDate.substring(0, 10) : '');
        $('#pm-save').html('<i class="fas fa-save me-1"></i>Save Changes');
        $('#pm-ai-row').removeClass('d-none').addClass('d-flex');
        $('#pm-ai-hint').addClass('d-none');
        this._loadCategories(product.categoryId || null);
        this._modal.show();
    },

    async _save() {
        const name = $('#pm-name').val().trim();
        const slug = $('#pm-slug').val().trim();
        const price = parseFloat($('#pm-price').val());

        if (!name || !slug || isNaN(price) || price <= 0) {
            Toast.error('Name, slug, and a valid price are required.');
            return;
        }

        const data = {
            name,
            slug,
            description: $('#pm-desc').val().trim() || null,
            price,
            stock: parseInt($('#pm-stock').val()) || 0,
            isActive: true,
            categoryId: $('#pm-category').val() ? parseInt($('#pm-category').val()) : null,
            discountStartDate: $('#pm-disc-start').val() ? new Date($('#pm-disc-start').val()).toISOString() : null,
            discountEndDate: $('#pm-disc-end').val() ? new Date($('#pm-disc-end').val()).toISOString() : null
        };

        const isEdit = !!this._editingId;
        const $btn = $('#pm-save').prop('disabled', true)
            .html('<i class="fas fa-spinner fa-spin me-1"></i>Saving...');

        try {
            if (isEdit) {
                await ProductService.updateProduct(this._editingId, data);
                Toast.success('Product updated successfully!');
            } else {
                await ProductService.addProduct(data);
                Toast.success('Product added successfully!');
            }
            this._modal.hide();
            // Refresh list if on product list page
            if (window.productPage) {
                await window.productPage.fetchAndRenderProducts();
            } else {
                // On detail page — go back to updated detail
                setTimeout(() => window.location.reload(), 800);
            }
        } catch (e) {
            Toast.error(e.message || 'Failed to save product. Please try again.');
        } finally {
            $btn.prop('disabled', false)
                .html(isEdit
                    ? '<i class="fas fa-save me-1"></i>Save Changes'
                    : '<i class="fas fa-plus me-1"></i>Add Product');
        }
    }
};

window.ProductAdminModal = ProductAdminModal;
