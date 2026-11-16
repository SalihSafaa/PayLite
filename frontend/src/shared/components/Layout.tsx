import { AppShell, Avatar, Burger, Drawer, Group, Menu, Stack, Text, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconChevronDown, IconFileText, IconLogout } from '@tabler/icons-react'
import { Link, NavLink, Outlet } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { NotificationBell } from '../../features/activity/NotificationBell'
import { BrandLogo } from './BrandMark'

function NavLinks({ isMerchant, onNavigate, vertical = false }: { isMerchant: boolean; onNavigate?: () => void; vertical?: boolean }) {
  const cls = vertical ? 'nav-link nav-link-vertical' : 'nav-link'
  return (
    <>
      {/* Owner A */}
      <NavLink to="/" end className={cls} onClick={onNavigate}>Wallet</NavLink>
      {/* Merchant accounts only receive money, so they get no Send page. */}
      {!isMerchant && <NavLink to="/send" className={cls} onClick={onNavigate}>Send money</NavLink>}
      <NavLink to="/history" className={cls} onClick={onNavigate}>History</NavLink>
      {/* Owner B */}
      <NavLink to="/notifications" className={cls} onClick={onNavigate}>Notifications</NavLink>
      {isMerchant && <NavLink to="/merchant/payments" className={cls} onClick={onNavigate}>Payments</NavLink>}
      {isMerchant && <NavLink to="/merchant/summary" className={cls} onClick={onNavigate}>Daily summary</NavLink>}
    </>
  )
}

export function Layout() {
  const { user, signOut } = useAuth()
  const isMerchant = user?.role === 'Merchant'
  const [menuOpen, menu] = useDisclosure(false)

  // Logged out (login / register): those pages bring their own full-screen layout.
  if (!user) {
    return (
      <main className="auth-shell">
        <Outlet />
      </main>
    )
  }

  return (
    <AppShell header={{ height: 64 }} padding={0}>
      <AppShell.Header className="app-header">
        <Group h="100%" px={{ base: 'md', sm: 'xl' }} justify="space-between" wrap="nowrap">
          <Group gap="xl" wrap="nowrap" h="100%">
            <Burger opened={menuOpen} onClick={menu.toggle} hiddenFrom="md" size="sm" aria-label="Open menu" />
            <BrandLogo />
            <Group component="nav" gap={4} wrap="nowrap" h="100%" visibleFrom="md" aria-label="Main">
              <NavLinks isMerchant={isMerchant} />
            </Group>
          </Group>
          <Group gap="sm" wrap="nowrap">
            <NotificationBell />
            <Menu position="bottom-end" width={200} shadow="md">
              <Menu.Target>
                <UnstyledButton className="user-button" aria-label={`Account menu for ${user.username}`}>
                  <Group gap={8} wrap="nowrap">
                    <Avatar size={32} radius="xl" color="plum" variant="filled">
                      {user.username.slice(0, 1).toUpperCase()}
                    </Avatar>
                    <Text size="sm" fw={500} visibleFrom="sm" maw={140} truncate>{user.username}</Text>
                    <IconChevronDown size={16} stroke={1.75} aria-hidden="true" />
                  </Group>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{isMerchant ? 'Merchant account' : 'Personal account'}</Menu.Label>
                <Menu.Item leftSection={<IconFileText size={16} stroke={1.75} />} component={Link} to="/policy">
                  Wallet policy
                </Menu.Item>
                <Menu.Item leftSection={<IconLogout size={16} stroke={1.75} />} onClick={signOut}>
                  Log out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>

      <Drawer opened={menuOpen} onClose={menu.close} size="xs" title={<BrandLogo />} hiddenFrom="md">
        <Stack component="nav" gap={2} aria-label="Main">
          <NavLinks isMerchant={isMerchant} onNavigate={menu.close} vertical />
        </Stack>
      </Drawer>

      <AppShell.Main className="app-main">
        <div className="page">
          <Outlet />
        </div>
      </AppShell.Main>
    </AppShell>
  )
}
