import axios from 'axios';

const API_BASE = 'https://localhost:7002/api';

/**
 * Queries the .NET API which internally searches Elasticsearch.
 * Returns a list of records matching the keyword.
 */
export const searchElasticsearch = async (query) => {
    try {
        const response = await axios.get(`${API_BASE}/search`, {
            params: { q: query }
        });
        return response.data; // Expected: Array of { id, fileName, company, date, etc }
    } catch (error) {
        console.error("Elasticsearch query failed:", error);
        throw error;
    }
};