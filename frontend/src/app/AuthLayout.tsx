import { Box, Stack, Text, Title } from '@mantine/core'
import type { ReactNode } from 'react'
import { BrandLogo, BrandMark } from '../shared/components/BrandMark'

/** Login and register: the brand panel on the left (hidden on phones), the form on the right. */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <div className="auth-split">
      <aside className="auth-brand" aria-hidden="true">
        <BrandLogo inverted />
        <div className="auth-brand-mark">
          <BrandMark size={260} />
        </div>
        <Stack gap="xs" maw={380}>
          <Title order={2} c="white" fz={34} lh={1.15}>Send dinars to anyone, in seconds.</Title>
          <Text c="plum.1" fz="lg">
            A wallet for people and shops in Iraq. Pay a friend, pay a merchant, and see it arrive live.
          </Text>
        </Stack>
      </aside>
      <section className="auth-form">
        <Box w="100%" maw={400}>
          <Box hiddenFrom="md" mb="xl">
            <BrandLogo />
          </Box>
          <Title order={1} fz={28}>{title}</Title>
          <Text c="dimmed" mt={4} mb="xl">{subtitle}</Text>
          {children}
        </Box>
      </section>
    </div>
  )
}
