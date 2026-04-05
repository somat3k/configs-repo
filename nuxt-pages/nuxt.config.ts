// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  devtools: { enabled: true },

  // Static site generation for GitHub Pages
  ssr: true,
  nitro: {
    preset: 'github-pages',
    prerender: {
      failOnError: true,
      ignore: ['/architecture', '/modules', '/api', '/checklist'],
    },
  },

  // GitHub Pages base URL
  app: {
    baseURL: process.env.NUXT_PUBLIC_BASE_URL || '/configs-repo/',
    head: {
      title: 'MLS — Machine Learning Studio for Trading',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        {
          name: 'description',
          content:
            'Machine Learning Studio for Trading, Arbitrage, and DeFi — enterprise-grade distributed platform documentation.',
        },
      ],
      link: [{ rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' }],
    },
  },

  modules: [
    '@nuxt/content',
    '@nuxtjs/tailwindcss',
    '@nuxt/icon',
  ],

  content: {
    documentDriven: true,
    highlight: {
      theme: 'github-dark',
      langs: ['csharp', 'python', 'typescript', 'json', 'yaml', 'sql', 'bash', 'dockerfile'],
    },
  },

  runtimeConfig: {
    public: {
      siteUrl: process.env.NUXT_PUBLIC_SITE_URL || 'https://somat3k.github.io/configs-repo',
    },
  },

  compatibilityDate: '2024-11-01',
})
