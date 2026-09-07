const ProductService = {
    baseUrl: `${window.API_BASE_URL}/api/product`,

    async getProductList(request) {
        const response = await $.ajax({
            url: `${this.baseUrl}/get-product-list`,
            method: 'GET',
            data: request,                       // jQuery serialises as query string
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to get products');
    },

    async getProductById(id) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${id}`,
            method: 'GET',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Product not found');
    },

    async addProduct(product) {
        const response = await $.ajax({
            url: `${this.baseUrl}/add-product`,
            method: 'POST',
            contentType: 'application/json',
            headers: ServiceUtils.getHeaders(),
            data: JSON.stringify(product)
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to add product');
    },

    async updateProduct(id, product) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${id}`,
            method: 'PUT',
            contentType: 'application/json',
            headers: ServiceUtils.getHeaders(),
            data: JSON.stringify(product)
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to update product');
    },

    async deleteProduct(id) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${id}`,
            method: 'DELETE',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return true;
        throw new Error(response.errorMessage || 'Failed to delete product');
    }
};

window.ProductService = ProductService; 