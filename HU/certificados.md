una vez completa la capacitación debe generarse el certificado, el certificado debe contar con las firmas de las personas que auspician la capacitación, por tanto una capacticacion debe tener como agregar los responsables. El instructor siempre figura en la firma pero pueden existir uno o más responsables adiconales.

El formato del certificado te lo he dejando en este directorio en certificado.png Junto con el fondo.png donde consta el logo de DOS el borde del certificado y el espacio donde ira el texto. El texto lo debes incluir con html y finalmente convertir a pdf. Para ello crearemos un docker denominado emisor_documentos que se encargara de generar los certificados y los colocara en una carpeta denominada /output para que luego otro docker pueda usarlos y enviarlos o entregarmelos como lo decidamos más adelante.

La capacitacion puede ser de tipo Partificación o Aprobación. Y eso se reflejara en el certificado.

El certificado debe llevar bajo el texto de certiifcado el tipo de participación, luego el nombre del asistente a la capacitacion, y seguido el texto: Ha completado con éxito (la charla o el curso o el seminario segun corresponda) sobre (tema de la capacitacion).
Dictado el (fecha de la capacitacion) con una duración de(duración de la capactiacion) horas.

Finalmente cerramos con las firmas, nombres cargos y empresa de cada responsable.
 