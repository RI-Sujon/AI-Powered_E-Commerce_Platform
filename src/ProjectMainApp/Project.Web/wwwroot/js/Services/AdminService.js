const AdminService = {
    baseUrl: `${window.API_BASE_URL}/api/admin`,

    async getStats() {
        const response = await $.ajax({
            url: `${this.baseUrl}/stats`,
            method: 'GET',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to load stats');
    },

    async getAllOrders(page, pageSize, status) {
        const response = await $.ajax({
            url: `${this.baseUrl}/orders`,
            method: 'GET',
            headers: ServiceUtils.getHeaders(),
            data: { page: page || 1, pageSize: pageSize || 20, status: status || '' }
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to load orders');
    }
};

window.AdminService = AdminService;
