const CategoryService = {
    baseUrl: `${window.API_BASE_URL}/api/category`,

    async getCategories() {
        const response = await $.ajax({
            url: this.baseUrl,
            method: 'GET',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to load categories');
    }
};

window.CategoryService = CategoryService;
