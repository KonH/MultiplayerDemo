import { defineConfig } from 'vitest/config';

export default defineConfig({
	server: {
		port: 5173,
		strictPort: true
	},
	build: {
		outDir: 'dist',
		target: 'es2022'
	},
	test: {
		environment: 'node',
		include: ['tests/**/*.test.ts']
	}
});
