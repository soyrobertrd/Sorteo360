import type { CampanaIdioma, CampanaTipo } from './mockParticipants'

/** Las plantillas de correo de las campañas, por idioma y tipo. */
export interface PlantillaCorreo {
  tipo: CampanaTipo
  idioma: CampanaIdioma
  asunto: string
  cuerpo: string
}

export const PLANTILLAS_MULTI_IDIOMA: Record<
  CampanaIdioma,
  Partial<Record<CampanaTipo | 'Personalizada', { asunto: string; cuerpo: string }>>
> = {
  ES: {
    'Save the Date': {
      asunto: 'Reserva la Fecha: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Le invitamos a reservar en su agenda la fecha para el evento {{evento}}, organizado por el Comité Organizador.

Fecha programada: {{fecha}}

Próximamente le estaremos enviando la invitación oficial con el enlace de registro y acreditación.

Atentamente,
Comité Organizador`,
    },
    'Apertura de Registro': {
      asunto: 'Invitación Oficial: {{evento}} - Registro Abierto',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Nos complace invitarle oficialmente a participar en el evento {{evento}}.

El evento se llevará a cabo el {{fecha}}. Por favor confirme su asistencia completando su registro aquí:
{{checkin_url}}

Al completar su registro, recibirá su Pase Digital con Código QR.

Atentamente,
Organización Anfitriona`,
    },
    'Venue Confirmado': {
      asunto: 'Sede y Logística Confirmada: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Le informamos que la sede oficial para el evento {{evento}} ha sido confirmada exitosamente.

Consulte la ubicación y detalles de acceso ingresando aquí:
{{checkin_url}}

Atentamente,
Comité Organizador`,
    },
    Reminder: {
      asunto: 'Recordatorio Importante: Confirma tu asistencia a {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Le recordamos que el registro para {{evento}} está próximo a concluir.

Asegure su lugar ingresando en el enlace de acreditación:
{{checkin_url}}

¡Esperamos contar con su presencia!

Saludos cordiales,
Organización Anfitriona`,
    },
    'Cambio de Fecha': {
      asunto: 'Actualización de Fecha Oficial: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Le notificamos un cambio en la fecha oficial de celebración del evento {{evento}}.

Nueva Fecha: {{fecha}}

Agradecemos tomar nota en su agenda. Su registro existente sigue siendo válido: {{checkin_url}}`,
    },
    'Cambio de Venue': {
      asunto: 'Actualización de Sede: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Le notificamos que la sede para el evento {{evento}} ha sido actualizada.

Por favor consulte la nueva dirección aquí: {{checkin_url}}`,
    },
    'Pase VIP & Acreditación': {
      asunto: 'Acreditación Oficial & Pase de Acceso: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Nos complace confirmarle que su Pase de Acreditación para el evento {{evento}} ha sido emitido con éxito.

Adjunto encontrará su Código QR de acceso rápido. Por favor preséntelo en el área de registro al ingresar:
{{checkin_url}}

Atentamente,
Comité de Acreditación`,
    },
    'Convocatoria de Prensa & Medios': {
      asunto: 'Convocatoria Especial: {{evento}}',
      cuerpo: `Estimado/a representante ({{empresa}}),

Le invitamos cordialmente a la cobertura especial del evento {{evento}}.

Fecha y Hora: {{fecha}}
Acreditación de acceso: {{checkin_url}}

Agradecemos su participación.

Coordinación de Comunicaciones`,
    },
    'Agenda & Programa Oficial': {
      asunto: 'Agenda Oficial & Programa de Actividades: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Compartimos con usted el Programa Oficial de actividades para el evento {{evento}}.

Consulte el cronograma aquí:
{{checkin_url}}

Atentamente,
Coordinación de Contenido & Programa`,
    },
    'Carta de Agradecimiento': {
      asunto: 'Agradecimiento Oficial por su Participación en {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Deseamos expresarle nuestro más sincero agradecimiento por su destacada participación en el evento {{evento}}.

Su presencia y valioso aporte han contribuido significativamente al éxito de este encuentro corporativo.

Acceda a la memoria gráfica e información relevante del evento aquí:
{{checkin_url}}

Saluda cordialmente,
Organización Anfitriona`,
    },
    'Confirmación de RSVP': {
      asunto: 'Confirmación de Asistencia Registrada: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Hemos recibido con agrado la confirmación de su asistencia al evento {{evento}}.

Fecha del Evento: {{fecha}}
Pase de Acceso & Código QR: {{checkin_url}}

Le sugerimos guardar esta acreditación en su dispositivo móvil para agilizar su ingreso.

Atentamente,
Comité Organizador`,
    },
    'Guía del Participante & Protocolo': {
      asunto: 'Guía del Participante & Indicaciones: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Para asegurar una experiencia óptima durante {{evento}}, compartimos la Guía Oficial del Participante con recomendaciones de llegada y código de vestimenta.

Descargue la Guía del Participante aquí:
{{checkin_url}}

Atentamente,
Coordinación de Eventos`,
    },
    'Rueda de Negocios B2B': {
      asunto: 'Confirmación de Agenda & Reuniones: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Le notificamos que su agenda para {{evento}} ha sido confirmada exitosamente.

Consulte sus detalles aquí:
{{checkin_url}}

Atentamente,
Coordinación del Evento`,
    },
    'Actualización de Datos CRM (Agencias / Tour Operadores)': {
      asunto: 'Actualización Oficial de Directorio y Registro de Participantes',
      cuerpo: `Estimado/a {{empresa}},

Estamos actualizando nuestro directorio oficial de invitados y participantes corporativos.

Le invitamos a verificar y completar sus datos de contacto en el siguiente enlace:
{{checkin_url}}

Esta información permitirá mantener activa su acreditación para participar en nuestros próximos eventos y sorteos.

Agradecemos de antemano su colaboración.

Atentamente,
Organización Anfitriona`,
    },
    'Resumen & Highlights del Evento': {
      asunto: 'Memoria Gráfica & Resumen Oficial: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Reviva los momentos más destacados del evento {{evento}}. Ya se encuentra disponible la galería de fotos y resumen de resultados.

Acceda al resumen ingresando en:
{{checkin_url}}

¡Gracias por acompañarnos!
Organización Anfitriona`,
    },
    'Certificado de Asistencia': {
      asunto: 'Certificado Oficial de Asistencia: {{evento}}',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Le agradecemos su destacada participación en el evento {{evento}}.

Su Certificado Oficial de Asistencia ya se encuentra disponible para descarga aquí:
{{checkin_url}}

¡Gracias por acompañarnos!

Atentamente,
Organización Anfitriona`,
    },
    Posposición: {
      asunto: 'Aviso Importante: Reprogramación del evento {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Informamos que el evento {{evento}} ha sido reprogramado. Próximamente anunciaremos las nuevas fechas oficiales.`,
    },
    Cancelación: {
      asunto: 'Aviso de Cancelación: {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

Lamentamos informar que el evento {{evento}} ha sido cancelado. Agradecemos su comprensión.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Gracias por acompañarnos en {{evento}} · Encuesta de Satisfacción',
      cuerpo: `Estimado/a {{nombre}} ({{empresa}}),

Queremos expresar nuestro sincero agradecimiento por su valiosa participación en {{evento}}.

Le invitamos a compartir sus comentarios completando nuestra encuesta: {{checkin_url}}

Atentamente,
Organización Anfitriona`,
    },
    Personalizada: {
      asunto: 'Comunicado Oficial - {{evento}}',
      cuerpo: `Estimado/a {{nombre}},

[Escribe aquí tu mensaje personalizado para la campaña de convocatoria...]

Enlace de registro: {{checkin_url}}

Atentamente,
{{evento}} · Organización Anfitriona`,
    },
  },

  EN: {
    'Save the Date': {
      asunto: 'Save the Date: {{evento}}',
      cuerpo: `Dear {{nombre}} ({{empresa}}),

Please save the date for {{evento}}, organized by Host Organization.

Scheduled Date: {{fecha}}

Official invitation and registration details will follow shortly.

Best regards,
Event Organizing Committee`,
    },
    'Apertura de Registro': {
      asunto: 'Official Invitation: {{evento}} - Registration Open',
      cuerpo: `Dear {{nombre}} ({{empresa}}),

We are pleased to officially invite you to {{evento}}.

The event will take place on {{fecha}}. Please confirm your attendance by registering here:
{{checkin_url}}

Upon completing your registration, you will receive your Digital Access Pass with QR Code.

Sincerely,
Event Organizing Committee`,
    },
    'Venue Confirmado': {
      asunto: 'Confirmed Venue & Logistics: {{evento}}',
      cuerpo: `Dear {{nombre}} ({{empresa}}),

We are pleased to inform you that the official venue for {{evento}} has been confirmed.

View location details here:
{{checkin_url}}

Best regards,
Organizing Committee`,
    },
    Reminder: {
      asunto: 'Important Reminder: Confirm your attendance to {{evento}}',
      cuerpo: `Dear {{nombre}},

This is a friendly reminder that registration for {{evento}} is closing soon.

Secure your pass here:
{{checkin_url}}

We look forward to seeing you!

Best regards,
Host Organization`,
    },
    'Cambio de Fecha': {
      asunto: 'Date Update Notice: {{evento}}',
      cuerpo: `Dear {{nombre}},

Please be advised of an updated official date for {{evento}}.

New Date: {{fecha}}

Your existing registration remains valid: {{checkin_url}}`,
    },
    'Cambio de Venue': {
      asunto: 'Venue Location Update: {{evento}}',
      cuerpo: `Dear {{nombre}},

Please note that the venue location for {{evento}} has been updated.

View the updated directions here: {{checkin_url}}`,
    },
    Posposición: {
      asunto: 'Important Notice: Event Rescheduled - {{evento}}',
      cuerpo: `Dear {{nombre}},\n\nPlease be advised that {{evento}} has been rescheduled. New dates will be announced shortly.`,
    },
    Cancelación: {
      asunto: 'Cancellation Notice: {{evento}}',
      cuerpo: `Dear {{nombre}},\n\nWe regret to inform you that {{evento}} has been cancelled. Thank you for your understanding.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Thank you for joining us at {{evento}} · Satisfaction Survey',
      cuerpo: `Dear {{nombre}} ({{empresa}}),\n\nThank you for participating in {{evento}}. Please take a moment to share your feedback: {{checkin_url}}\n\nSincerely,\nHost Organization`,
    },
    Personalizada: {
      asunto: 'Official Announcement - {{evento}}',
      cuerpo: `Dear {{nombre}},\n\n[Write your custom campaign message here...]\n\nRegistration link: {{checkin_url}}\n\nBest regards,\n{{evento}} · Organizing Committee`,
    },
  },

  FR: {
    'Save the Date': {
      asunto: 'Réservez la Date: {{evento}}',
      cuerpo: `Cher/Chère {{nombre}} ({{empresa}}),\n\nNous vous invitons à réserver la date de l'événement {{evento}}.\n\nDate prévue: {{fecha}}\n\nL'invitation officielle suivra très prochainement.\n\nCordialement,\nComité d'Organisation`,
    },
    'Apertura de Registro': {
      asunto: 'Invitation Officielle: {{evento}} - Inscription Ouverte',
      cuerpo: `Cher/Chère {{nombre}} ({{empresa}}),\n\nNous avons le plaisir de vous inviter à l'événement {{evento}}.\n\nL'événement aura lieu le {{fecha}}. Veuillez confirmer votre présence ici:\n{{checkin_url}}\n\nCordialement,\nComité d'Organisation`,
    },
    'Venue Confirmado': {
      asunto: 'Lieu Officiel Confirmé: {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\nLe lieu officiel pour {{evento}} est désormais confirmé. Consultez la carte interactive ici: {{checkin_url}}`,
    },
    Reminder: {
      asunto: 'Rappel Important: Confirmez votre présence à {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\nNous vous rappelons que les inscriptions pour {{evento}} se terminent bientôt: {{checkin_url}}`,
    },
    'Cambio de Fecha': {
      asunto: 'Mise à Jour de la Date: {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\nVeuillez noter la nouvelle date pour {{evento}}: {{fecha}}.`,
    },
    'Cambio de Venue': {
      asunto: 'Mise à Jour du Lieu: {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\nLe lieu de l'événement {{evento}} a été modifié: {{checkin_url}}`,
    },
    Posposición: {
      asunto: 'Avis de Report: {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\nL'événement {{evento}} a été reporté. Les nouvelles dates seront communiquées sous peu.`,
    },
    Cancelación: {
      asunto: "Avis d'Annulation: {{evento}}",
      cuerpo: `Cher/Chère {{nombre}},\n\nNous avons le regret de vous informer que l'événement {{evento}} a été annulé.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Merci pour votre participation à {{evento}} · Enquête',
      cuerpo: `Cher/Chère {{nombre}},\n\nMerci d'avoir participé à {{evento}}. Merci de remplir notre questionnaire: {{checkin_url}}`,
    },
    Personalizada: {
      asunto: 'Communiqué Officiel - {{evento}}',
      cuerpo: `Cher/Chère {{nombre}},\n\n[Rédigez votre message personnalisé ici...]\n\nLien: {{checkin_url}}`,
    },
  },

  PT: {
    'Save the Date': {
      asunto: 'Reserve a Data: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}} ({{empresa}}),\n\nConvidamos você a reservar a data para o evento {{evento}}.\n\nData prevista: {{fecha}}\n\nEm breve enviaremos o link oficial de credenciamento.\n\nAtenciosamente,\nComissão Organizadora`,
    },
    'Apertura de Registro': {
      asunto: 'Convite Oficial: {{evento}} - Inscrições Abertas',
      cuerpo: `Prezado(a) {{nombre}} ({{empresa}}),\n\nTemos o prazer de convidá-lo(a) para o evento {{evento}}.\n\nData: {{fecha}}. Confirme sua presença aqui:\n{{checkin_url}}\n\nAtenciosamente,\nComissão Organizadora`,
    },
    'Venue Confirmado': {
      asunto: 'Local Confirmado: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nO local oficial para {{evento}} foi confirmado: {{checkin_url}}`,
    },
    Reminder: {
      asunto: 'Lembrete Importante: Confirme sua presença em {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nAs inscrições para {{evento}} estão se encerrando: {{checkin_url}}`,
    },
    'Cambio de Fecha': {
      asunto: 'Atualização de Data: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nNova data para {{evento}}: {{fecha}}.`,
    },
    'Cambio de Venue': {
      asunto: 'Atualização de Local: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nO local do evento foi alterado: {{checkin_url}}`,
    },
    Posposición: {
      asunto: 'Aviso de Adiamento: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nO evento {{evento}} foi adiado. Novas datas em breve.`,
    },
    Cancelación: {
      asunto: 'Aviso de Cancelamento: {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\nInformamos o cancelamento de {{evento}}. Agradecemos a compreensão.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Obrigado por participar de {{evento}} · Pesquisa',
      cuerpo: `Prezado(a) {{nombre}},\n\nObrigado por sua presença em {{evento}}. Preencha nossa pesquisa: {{checkin_url}}`,
    },
    Personalizada: {
      asunto: 'Comunicado Oficial - {{evento}}',
      cuerpo: `Prezado(a) {{nombre}},\n\n[Escreva sua mensagem personalizada aqui...]\n\nLink: {{checkin_url}}`,
    },
  },

  DE: {
    'Save the Date': {
      asunto: 'Terminvormerkung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}} ({{empresa}}),\n\nBitte merken Sie sich das Datum für {{evento}} vor.\n\nDatum: {{fecha}}\n\nOffizielle Einladung folgt in Kürze.`,
    },
    'Apertura de Registro': {
      asunto: 'Offizielle Einladung: {{evento}} - Anmeldung Geöffnet',
      cuerpo: `Sehr geehrte(r) {{nombre}} ({{empresa}}),\n\nWir laden Sie herzlich zu {{evento}} ein.\n\nDatum: {{fecha}}. Bitte bestätigen Sie Ihre Teilnahme hier: {{checkin_url}}`,
    },
    'Pase VIP & Acreditación': {
      asunto: 'Offizielle Akkreditierung & VIP-Pass: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nIhr VIP-Zugangspass für {{evento}} wurde ausgestellt: {{checkin_url}}`,
    },
    'Convocatoria de Prensa & Medios': {
      asunto: 'Presseeinladung: {{evento}}',
      cuerpo: `Sehr geehrte Medienvertreter,\n\nWir laden Sie herzlich zur Veranstaltung {{evento}} ein. Akkreditierung: {{checkin_url}}`,
    },
    'Agenda & Programa Oficial': {
      asunto: 'Offizielles Programm & Agenda: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nHier finden Sie das offizielle Veranstaltungsprogramm für {{evento}}: {{checkin_url}}`,
    },
    'Confirmación de RSVP': {
      asunto: 'Bestätigung Ihrer Teilnahme: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nVielen Dank für Ihre Zusage zu {{evento}}. Ihr Ticket: {{checkin_url}}`,
    },
    'Guía del Participante & Protocolo': {
      asunto: 'Teilnehmerleitfaden & Hinweise: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nHier finden Sie den Teilnehmerleitfaden für {{evento}}: {{checkin_url}}`,
    },
    'Rueda de Negocios B2B': {
      asunto: 'Terminbestätigung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nIhre Termine für {{evento}} wurden vereinbart: {{checkin_url}}`,
    },
    'Venue Confirmado': {
      asunto: 'Bestätigter Veranstaltungsort: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nDer Ort für {{evento}} steht fest: {{checkin_url}}`,
    },
    Reminder: {
      asunto: 'Wichtige Erinnerung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nErinnerung an die Anmeldung zu {{evento}}: {{checkin_url}}`,
    },
    'Cambio de Fecha': {
      asunto: 'Datumsaktualisierung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nNeues Datum für {{evento}}: {{fecha}}.`,
    },
    'Cambio de Venue': {
      asunto: 'Ortänderung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nNeuer Ort für {{evento}}: {{checkin_url}}`,
    },
    Posposición: {
      asunto: 'Verschiebung: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\n{{evento}} wurde verschoben. Neue Termine folgen.`,
    },
    Cancelación: {
      asunto: 'Absage: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nLeider muss {{evento}} abgesagt werden.`,
    },
    'Carta de Agradecimiento': {
      asunto: 'Vielen Dank für Ihre Teilnahme an {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}} ({{empresa}}),\n\nWir danken Ihnen herzlich für Ihre Teilnahme an {{evento}}.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Ihr Feedback zu {{evento}} · Umfrage',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nVielen Dank für Ihre Teilnahme. Bitte füllen Sie unsere Umfrage aus: {{checkin_url}}`,
    },
    'Resumen & Highlights del Evento': {
      asunto: 'Rückblick & Highlights: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nEntdecken Sie die Highlights von {{evento}}: {{checkin_url}}`,
    },
    'Certificado de Asistencia': {
      asunto: 'Teilnahmezertifikat: {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\nIhr Zertifikat für {{evento}} steht zum Download bereit: {{checkin_url}}`,
    },
    Personalizada: {
      asunto: 'Offizielle Mitteilung - {{evento}}',
      cuerpo: `Sehr geehrte(r) {{nombre}},\n\n[Ihre Nachricht hier...]\n\nLink: {{checkin_url}}`,
    },
  },

  IT: {
    'Save the Date': {
      asunto: 'Salva la Data: {{evento}}',
      cuerpo: `Gentile {{nombre}} ({{empresa}}),\n\nLa invitiamo a riservare la data per l'evento {{evento}}.\n\nData: {{fecha}}\n\nSeguirà invito ufficiale.`,
    },
    'Apertura de Registro': {
      asunto: 'Invito Ufficiale: {{evento}} - Registrazioni Aperte',
      cuerpo: `Gentile {{nombre}} ({{empresa}}),\n\nSiamo lieti di invitarla all'evento {{evento}}.\n\nData: {{fecha}}. Confermi la sua partecipazione qui: {{checkin_url}}`,
    },
    'Pase VIP & Acreditación': {
      asunto: 'Accreditamento Ufficiale & Pass VIP: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nIl suo Pass VIP per {{evento}} è pronto: {{checkin_url}}`,
    },
    'Convocatoria de Prensa & Medios': {
      asunto: 'Convocatoria Stampa: {{evento}}',
      cuerpo: `Gentili rappresentanti della stampa,\n\nVi invitiamo all'evento {{evento}}. Accredito: {{checkin_url}}`,
    },
    'Agenda & Programa Oficial': {
      asunto: 'Programma Ufficiale & Agenda: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nConsulti l'agenda ufficiale per {{evento}} qui: {{checkin_url}}`,
    },
    'Confirmación de RSVP': {
      asunto: 'Conferma di Partecipazione: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nGrazie per aver confermato la sua presenza a {{evento}}. Il suo pass: {{checkin_url}}`,
    },
    'Guía del Participante & Protocolo': {
      asunto: 'Guida del Partecipante & Protocollo: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nScarichi la guida ufficiale del partecipante per {{evento}}: {{checkin_url}}`,
    },
    'Rueda de Negocios B2B': {
      asunto: 'Conferma Agenda: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nI suoi appuntamenti per {{evento}} sono stati confermati: {{checkin_url}}`,
    },
    'Venue Confirmado': {
      asunto: 'Sede Ufficiale Confirmata: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nLa sede per {{evento}} è stata confermata: {{checkin_url}}`,
    },
    Reminder: {
      asunto: 'Promemoria Importante: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nLe ricordiamo che le registrazioni per {{evento}} stanno per chiudersi: {{checkin_url}}`,
    },
    'Cambio de Fecha': {
      asunto: 'Aggiornamento Data: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nNuova data per {{evento}}: {{fecha}}.`,
    },
    'Cambio de Venue': {
      asunto: 'Aggiornamento Sede: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nLa sede di {{evento}} è stata modificata: {{checkin_url}}`,
    },
    Posposición: {
      asunto: 'Avviso di Rinvio: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nL'evento {{evento}} è stato rinviato. Nuove date a breve.`,
    },
    Cancelación: {
      asunto: 'Avviso di Annullamento: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nCi rammarichiamo di informare che l'evento {{evento}} è stato annullato.`,
    },
    'Carta de Agradecimiento': {
      asunto: 'Ringraziamento Ufficiale per la Partecipazione a {{evento}}',
      cuerpo: `Gentile {{nombre}} ({{empresa}}),\n\nLa ringraziamo cordialmente per la sua preziosa partecipazione a {{evento}}.`,
    },
    'Encuesta Post Evento': {
      asunto: 'Valutazione Evento {{evento}} · Sondaggio',
      cuerpo: `Gentile {{nombre}},\n\nGrazie per la sua presenza. Compili il nostro sondaggio: {{checkin_url}}`,
    },
    'Resumen & Highlights del Evento': {
      asunto: 'Highlights & Galleria Fotografica: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nRiviva i momenti migliori di {{evento}}: {{checkin_url}}`,
    },
    'Certificado de Asistencia': {
      asunto: 'Certificato di Partecipazione: {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\nIl suo certificato per {{evento}} è pronto per il download: {{checkin_url}}`,
    },
    Personalizada: {
      asunto: 'Comunicato Ufficiale - {{evento}}',
      cuerpo: `Gentile {{nombre}},\n\n[Inserisca il messaggio qui...]\n\nLink: {{checkin_url}}`,
    },
  },
}

// Fallback por defecto si se requiere en español
export const PLANTILLAS_DEFAULT = PLANTILLAS_MULTI_IDIOMA.ES
