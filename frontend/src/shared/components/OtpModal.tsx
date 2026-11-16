import { Button, Group, Modal, Paper, PinInput, Stack, Text } from '@mantine/core'
import { IconInfoCircle, IconShieldLock } from '@tabler/icons-react'
import { useState } from 'react'

export const OTP_LENGTH = 6
/** Header the Wallet API reads the code from (OtpOptions.HeaderName on the server). */
export const OTP_HEADER = 'X-OTP-Code'
/** ProblemDetails.type the Wallet API returns when the code is missing or wrong (OtpOptions.ProblemType). */
export const OTP_PROBLEM_TYPE = 'urn:paylite:otp-invalid'

/**
 * Asks for the 6-digit one-time code before a sensitive action (sending money, showing the card details).
 * DEMO: no SMS is sent; the server accepts 123456, and the dialog says so.
 * The parent does the request: this component only collects the code and shows the result.
 */
type OtpModalProps = {
  opened: boolean
  title: string
  description: string
  confirmLabel: string
  submitting: boolean
  /** The server said the code was wrong (shown under the boxes). */
  error: string | null
  onConfirm: (code: string) => void
  onClose: () => void
}

export function OtpModal({ opened, title, onClose, submitting, ...form }: OtpModalProps) {
  return (
    <Modal
      opened={opened}
      onClose={() => {
        if (!submitting) onClose()
      }}
      title={
        <Group gap="xs">
          <IconShieldLock size={20} stroke={1.75} color="var(--mantine-color-plum-7)" aria-hidden="true" />
          <Text fw={600}>{title}</Text>
        </Group>
      }
      centered
      radius="lg"
    >
      {/* Mantine unmounts this while the dialog is closed, so every opening starts with empty boxes. */}
      <OtpForm submitting={submitting} {...form} />
    </Modal>
  )
}

function OtpForm({ description, confirmLabel, submitting, error, onConfirm }: Omit<OtpModalProps, 'opened' | 'title' | 'onClose'>) {
  const [code, setCode] = useState('')
  const complete = code.length === OTP_LENGTH

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        if (complete && !submitting) onConfirm(code)
      }}
    >
      <Stack gap="md">
        <Text size="sm" c="dimmed">{description}</Text>
        <Group justify="center">
          <PinInput
            length={OTP_LENGTH}
            type="number"
            oneTimeCode
            size="lg"
            radius="md"
            value={code}
            onChange={setCode}
            error={!!error}
            disabled={submitting}
            data-autofocus
            getInputProps={(index) => ({ 'aria-label': `Digit ${index + 1} of ${OTP_LENGTH}` })}
          />
        </Group>
        {error && (
          <Text size="sm" c="red.8" ta="center" role="alert">{error}</Text>
        )}
        {/* A note, not an alert: nothing is wrong. */}
        <Paper bg="plum.0" radius="md" p="sm">
          <Group gap="xs" wrap="nowrap">
            <IconInfoCircle size={18} stroke={1.75} color="var(--mantine-color-plum-7)" aria-hidden="true" />
            <Text size="sm">Demo: no code is sent to your phone. Use <strong>123456</strong>.</Text>
          </Group>
        </Paper>
        <Button type="submit" size="md" fullWidth disabled={!complete || submitting} loading={submitting}>
          {confirmLabel}
        </Button>
      </Stack>
    </form>
  )
}
